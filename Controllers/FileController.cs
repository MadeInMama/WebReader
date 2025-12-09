using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Helpers;
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
        var res = (await bucketRepository.GetAllAvailableBucketsAsync(User.GetUserRoles()))
            .Select(bucket =>
                new AllBucketsItem
                {
                    Name = bucket.Name,
                    CustomName = bucket.Name,
                    DateTime = bucket.CreatedDate.UtcDateTime
                });

        return View(new AllBucketsViewModel { Items = res });
    }

    public async Task<IActionResult> GetAllFilesInBucket(string bucketName, string orderBy = "CustomName")
    {
        var prop = typeof(File).GetProperty(orderBy);

        var res = (await fileRepository.GetAllAvailableObjectsInBucketAsync(bucketName, User.GetUserRoles()))
            .Select(obj => new AllFilesInBucketItem
            {
                Name = obj.Name,
                CustomName = obj.CustomName ?? obj.Name,
                DateTime = obj.UpdatedDate.UtcDateTime,
                Size = obj.Size ?? 0
            }).OrderBy(f => prop?.GetValue(f, null) ?? f.CustomName);

        return View(new AllFilesInBucketViewModel { BucketId = bucketName, Items = res });
    }

    public async Task<IActionResult> GetReading()
    {
        var userGuid = User.GetUserGuid();

        var readings = (await readingRepository.AllAsync(f => f.UserId == userGuid &&
                                                              !f.File!.IsHidden &&
                                                              f.File.AccessRoles.Intersect(User.GetUserRoles()).Any(),
            f => f.File!,
            f => f.File!.Bucket!)).ToList();

        var res = readings
            .GroupBy(reading => reading.File?.Bucket?.Name)
            .ToDictionary(reading => reading.Key!, reading => reading.Select(r => new AllFilesReadingItem
            {
                Name = r.File?.Name ?? "",
                CustomName = r.File?.CustomName ?? r.File?.Name ?? "",
                DateTime = r.File?.UpdatedDate.UtcDateTime ?? DateTime.UtcNow,
                Size = r.File?.Size ?? 0,
                Page = r.Page
            }));

        return View(new AllFilesReadingViewModel { Items = res });
    }

    public async Task<IActionResult> GetFile(string bucketName, string fileName)
    {
        var file = await fileRepository.FirstOrDefaultAsync(f =>
            f.Bucket!.Name == bucketName &&
            f.Name == fileName &&
            f.AccessRoles.Intersect(User.GetUserRoles()).Any());

        if (file == null) return RedirectToAction("AccessDenied", "Account");

        var userGuid = User.GetUserGuid();

        var reading = await readingRepository
            .FirstOrDefaultAsync(f => f.User!.Id == userGuid
                                      && f.File!.Bucket!.Name == bucketName
                                      && f.File!.Name == fileName);

        int? authValidSeconds = null;
        var authProps = (await HttpContext.AuthenticateAsync()).Properties;
        if (authProps is { ExpiresUtc: not null, IssuedUtc: not null })
            authValidSeconds = (int)authProps.ExpiresUtc.Value.Subtract(authProps.IssuedUtc.Value).TotalSeconds;

        var res = new FileViewModel
        {
            UserId = userGuid,
            FileId = file.Id,
            Page = reading?.Page ?? 1,
            Scale = reading?.Scale ?? 1,
            Title = file.CustomName ?? fileName,
            Url = await minioService.GetFileUrlAsync(bucketName, fileName),
            SendUpdateInSeconds = authValidSeconds
        };

        switch (file.Type)
        {
            case FileType.Pdf:
                return View("GetFilePdf", res);
            case FileType.Txt:
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
