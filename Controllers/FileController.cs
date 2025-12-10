using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;
using File = System.IO.File;

namespace WebReader.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public class FileController(
    MinioService minioService,
    BucketRepository bucketRepository,
    FileRepository fileRepository,
    UserReadingRepository readingRepository) : Controller
{
    public async Task<IActionResult> GetAllBuckets()
    {
        var res = (await bucketRepository.GetAllAvailableBucketsAsync(User.GetUserRoles(), User.GetUserGuid()))
            .Select(bucket =>
                new AllBucketsItem
                {
                    Name = bucket.Name,
                    CustomName = bucket.CustomName ?? bucket.Name,
                    DateTime = bucket.CreatedDate
                });

        return View(new AllBucketsViewModel { Items = res });
    }

    public async Task<IActionResult> GetAllFilesInBucket(string bucketName, string orderBy = "CustomName")
    {
        var prop = typeof(File).GetProperty(orderBy);

        var bucket = await bucketRepository
            .FirstOrDefaultAsync(f => f.IsAvailable &&
                                      !f.IsHidden &&
                                      f.Name == bucketName &&
                                      f.AccessRoles.Intersect(User.GetUserRoles()).Any() &&
                                      (f.UserId == User.GetUserGuid() || f.UserId == null));

        if (bucket == null) return RedirectToAction("AccessDenied", "Account");

        var res = (await fileRepository.GetAllAvailableObjectsInBucketAsync(bucketName, User.GetUserRoles()))
            .Select(obj => new AllFilesInBucketItem
            {
                Name = obj.Name,
                CustomName = obj.CustomName ?? obj.Name,
                DateTime = obj.UpdatedDate,
                Size = obj.Size ?? 0
            }).OrderBy(f => prop?.GetValue(f, null) ?? f.CustomName);

        return View(new AllFilesInBucketViewModel
            { BucketId = bucketName, BucketName = bucket.CustomName ?? bucketName, Items = res });
    }

    public async Task<IActionResult> GetReading()
    {
        var userGuid = User.GetUserGuid();

        var allUserReadings = await readingRepository.AllAsync(f => f.UserId == userGuid);

        var allUserReadingsGroupedByFile = allUserReadings
            .GroupBy(reading => reading.FileId)
            .ToDictionary(reading => reading.Key, reading => reading.ToList());

        var idsToDelete = new List<Guid>();
        var readingsToUpdate = new List<UserReading>();

        foreach (var readingGroup in allUserReadingsGroupedByFile.Where(f => f.Value.Count > 1))
        {
            var readingsOrdered = readingGroup.Value.OrderBy(f => f.CreatedDate).ToList();
            var picked = readingsOrdered.First();

            picked.Page = readingGroup.Value.Max(reading => reading.Page);
            picked.Scale = readingGroup.Value.OrderByDescending(f => f.UpdatedDate).First().Scale;

            readingsToUpdate.Add(picked);
            idsToDelete.AddRange(readingsOrdered.Skip(1).Select(reading => reading.Id));
        }

        var delete = idsToDelete.Count != 0
            ? readingRepository.DeleteAllAsync(idsToDelete)
            : Task.CompletedTask;

        var update = readingsToUpdate.Count != 0
            ? readingRepository.SetCurrPageAndScaleAsync(readingsToUpdate)
            : Task.CompletedTask;

        await Task.WhenAll(delete, update);

        var readings = (await readingRepository.AllAsync(f => f.UserId == userGuid &&
                                                              !f.File!.IsHidden &&
                                                              f.File.AccessRoles.Intersect(User.GetUserRoles()).Any(),
            f => f.File!,
            f => f.File!.Bucket!)).ToList();

        var res = readings.GroupBy(reading => new AllFilesReadingItemKey
            {
                Name = reading.File?.Bucket?.Name!,
                CustomName = reading.File?.Bucket?.CustomName ?? reading.File?.Bucket?.Name!
            })
            .ToDictionary(reading => reading.Key!, reading => reading
                .OrderByDescending(f => f.UpdatedDate)
                .Select(r => new AllFilesReadingItem
                {
                    Id = r.Id,
                    Name = r.File?.Name ?? "",
                    CustomName = r.File?.CustomName ?? r.File?.Name ?? "",
                    DateTime = r.UpdatedDate,
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
            .FirstOrDefaultAsync(f => f.UserId == userGuid
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
