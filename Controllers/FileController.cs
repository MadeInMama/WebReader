using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Minio.DataModel;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public class FileController(
    IMinioService minioService,
    UserReadingRepository readingRepository,
    FileRepository fileRepository) : Controller
{
    public async Task<IActionResult> GetAllBuckets()
    {
        var roles = User.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .Select(Enum.Parse<RoleType>)
            .ToList();

        var bucketNames = await fileRepository.GetAllAvailableBucketsAsync(roles);
        var buckets = await minioService.ListBucketsAsync();

        var filteredBuckets = buckets.Where(f => bucketNames.Contains(f.Name));

        return View(new AllBucketsViewModel { Buckets = filteredBuckets });
    }

    public async Task<IActionResult> GetAllFilesInBucket(string bucketId)
    {
        var roles = User.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .Select(Enum.Parse<RoleType>)
            .ToList();

        var objectNames = await fileRepository.GetAllAvailableObjectsInBucketAsync(bucketId, roles);
        var objects = await minioService.ListObjectsAsync(bucketId);

        var filteredObjects = objects.Where(f => objectNames.Contains(f.Key));

        return View(new AllFilesInBucketViewModel { BucketId = bucketId, Files = filteredObjects });
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

        var readings = await readingRepository.AllAsync(f => f.UserId == userGuid &&
                                                             !f.File!.IsHidden &&
                                                             f.File.AccessRoles.Intersect(roles).Any());
        var allBucketsFromDb = readings.Select(f => f.File!.Bucket).Distinct();
        var allObjectsFromDb = readings.Select(f => f.File!.Object).Distinct();
        var allBuckets = (await minioService.ListBucketsAsync())
            .Where(f => allBucketsFromDb.Contains(f.Name))
            .Select(f => f.Name);

        var res = new Dictionary<string, IEnumerable<Item>>();

        foreach (var bucket in allBuckets)
        {
            var allObjects = await minioService.ListObjectsAsync(bucket!);
            var readingObjects = allObjects.Where(f => allObjectsFromDb.Contains(f.Key));

            res.Add(bucket!, readingObjects);
        }

        return View(new AllFilesReadingViewModel { BucketFiles = res });
    }

    public async Task<IActionResult> GetFile(string bucketId, string objectName)
    {
        var roles = User.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .Select(Enum.Parse<RoleType>)
            .ToList();

        var file = await fileRepository.FirstOrDefaultAsync(f =>
            f.Bucket == bucketId && f.Object == objectName && f.AccessRoles.Intersect(roles).Any());

        if (file == null) return RedirectToAction("AccessDenied", "Account");

        var userGuidClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
        var userGuid = Guid.Parse(userGuidClaim.Value);
        var reading = await readingRepository
            .FirstOrDefaultAsync(f => f.User!.Id == userGuid
                                      && f.File!.Bucket == bucketId
                                      && f.File!.Object == objectName);

        int? authValidSeconds = null;
        var authProps = (await HttpContext.AuthenticateAsync()).Properties;
        if (authProps is { ExpiresUtc: not null, IssuedUtc: not null })
            authValidSeconds = (int)authProps.ExpiresUtc.Value.Subtract(authProps.IssuedUtc.Value).TotalSeconds;

        return View(new FileViewModel
        {
            UserId = userGuid,
            FileId = file.Id,
            Page = reading?.Page ?? 1,
            Title = objectName,
            Url = await minioService.GetFileUrlAsync(bucketId, objectName),
            SendUpdateInSeconds = authValidSeconds
        });
    }
}