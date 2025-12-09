using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public class FileController(
    IMinioService minioService,
    BucketRepository bucketRepository,
    FileRepository fileRepository,
    UserReadingRepository readingRepository) : Controller
{
    public async Task<IActionResult> GetAllBuckets()
    {
        var roles = User.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .Select(Enum.Parse<RoleType>)
            .ToList();

        var allAvailableBucketsForUser = await bucketRepository.GetAllAvailableBucketsAsync(roles);
        var allBucketsInS3 = await minioService.ListBucketsAsync();

        var intersectedBuckets =
            allBucketsInS3.Where(f => allAvailableBucketsForUser.Select(bucket => bucket.Name).Contains(f.Name));

        var res = intersectedBuckets.Select(bucket => new AllBucketsItem
        {
            Name = bucket.Name,
            CustomName = bucket.Name, //TODO: store buckets in db and autoupdate availability to simplify execution
            DateTime = bucket.CreationDateDateTime
        });

        return View(new AllBucketsViewModel { Items = res });
    }

    public async Task<IActionResult> GetAllFilesInBucket(string bucketName)
    {
        var roles = User.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .Select(Enum.Parse<RoleType>)
            .ToList();

        var allAvailableObjectsForUser =
            (await fileRepository.GetAllAvailableObjectsInBucketAsync(bucketName, roles)).ToList();
        var allAvailableObjectsForUserKeys = allAvailableObjectsForUser.Select(f => f.Name).Distinct();
        var allObjectsInS3 = await minioService.ListObjectsAsync(bucketName);

        var intersectedObjects = allObjectsInS3.Where(f => allAvailableObjectsForUserKeys.Contains(f.Key));

        var res = intersectedObjects.Select(obj => new AllFilesInBucketItem
        {
            Name = obj.Key,
            CustomName =
                allAvailableObjectsForUser.FirstOrDefault(f => f.Name.Equals(obj.Key))?.CustomName ??
                obj.Key, //TODO: autoupdate availability to simplify execution
            DateTime = obj.LastModifiedDateTime ?? DateTime.Now,
            Size = obj.Size
        });

        return View(new AllFilesInBucketViewModel { BucketId = bucketName, Items = res });
    }

    public async Task<IActionResult> GetReading()
    {
        var roles = User.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .Select(Enum.Parse<RoleType>)
            .ToList();
        var userGuidClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
        var userGuid = Guid.Parse(userGuidClaim.Value);

        var readings = (await readingRepository.AllAsync(f => f.UserId == userGuid &&
                                                              !f.File!.IsHidden &&
                                                              f.File.AccessRoles.Intersect(roles).Any(),
            f => f.File!, f => f.File!.Bucket!)).ToList();
        var allBucketsFromDb = readings.Select(f => f.File!.Bucket!.Name).Distinct();
        var allObjectsFromDb = readings.Select(f => f.File!.Name).Distinct();
        var allBuckets = (await minioService.ListBucketsAsync())
            .Where(f => allBucketsFromDb.Contains(f.Name))
            .Select(f => f.Name);

        var res = new Dictionary<string, IEnumerable<AllFilesReadingItem>>();

        foreach (var bucket in allBuckets)
        {
            var allObjects = await minioService.ListObjectsAsync(bucket!);
            var readingObjects = allObjects.Where(f => allObjectsFromDb.Contains(f.Key));

            res.Add(bucket!, readingObjects.Select(obj =>
            {
                var r = readings.FirstOrDefault(f => f.File!.Name.Equals(obj.Key));

                return new AllFilesReadingItem
                {
                    Name = obj.Key,
                    CustomName =
                        r?.File?.CustomName ?? obj.Key,
                    DateTime = obj.LastModifiedDateTime ?? DateTime.Now,
                    Size = obj.Size,
                    Page = r?.Page ?? 1
                };
            }));
        }

        return View(new AllFilesReadingViewModel { Items = res });
    }

    public async Task<IActionResult> GetFile(string bucketName, string fileName)
    {
        var roles = User.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .Select(Enum.Parse<RoleType>)
            .ToList();

        var file = await fileRepository.FirstOrDefaultAsync(f =>
            f.Bucket!.Name == bucketName && f.Name == fileName && f.AccessRoles.Intersect(roles).Any());

        if (file == null) return RedirectToAction("AccessDenied", "Account");

        var userGuidClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
        var userGuid = Guid.Parse(userGuidClaim.Value);
        var reading = await readingRepository
            .FirstOrDefaultAsync(f => f.User!.Id == userGuid
                                      && f.File!.Bucket!.Name == bucketName
                                      && f.File!.Name == fileName);

        int? authValidSeconds = null;
        var authProps = (await HttpContext.AuthenticateAsync()).Properties;
        if (authProps is { ExpiresUtc: not null, IssuedUtc: not null })
            authValidSeconds = (int)authProps.ExpiresUtc.Value.Subtract(authProps.IssuedUtc.Value).TotalSeconds;

        return View(new FileViewModel
        {
            UserId = userGuid,
            FileId = file.Id,
            Page = reading?.Page ?? 1,
            Scale = reading?.Scale ?? 1,
            Title = file.CustomName ?? fileName,
            Url = await minioService.GetFileUrlAsync(bucketName, fileName),
            SendUpdateInSeconds = authValidSeconds
        });
    }
}
