using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;
using File = WebReader.Models.Entities.File;

namespace WebReader.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public class FileController(
    BucketRepository bucketRepository,
    FileRepository fileRepository,
    UserReadingRepository readingRepository,
    MinioService minioService) : Controller
{
    public async Task<IActionResult> GetAllBuckets()
    {
        var res = (await bucketRepository.GetAllAvailableBucketsAsync(User.GetUserRoles(), User.GetUserGuid()))
            .Select(bucket =>
                new AllBucketsItem
                {
                    Id = bucket.Id,
                    CustomName = bucket.CustomName ?? bucket.Name,
                    DateTime = bucket.CreatedDate
                });

        return View(new AllBucketsViewModel { Items = res });
    }

    public async Task<IActionResult> GetAllFilesInBucket(Guid bucketId, string orderBy = "FileName")
    {
        var prop = typeof(AllFilesInBucketItem).GetProperty(orderBy);

        var bucket = await bucketRepository
            .FirstOrDefaultAsync(f => f.IsAvailable &&
                                      !f.IsHidden &&
                                      f.Id == bucketId &&
                                      f.AccessRoles.Intersect(User.GetUserRoles()).Any() &&
                                      (f.UserId == User.GetUserGuid() || f.UserId == null));

        if (bucket == null) return RedirectToAction("AccessDenied", "Account");

        var userGuid = User.GetUserGuid();

        var allUserReadings = await readingRepository.AllAsync(f => f.UserId == userGuid);

        var res = (await fileRepository.GetAllAvailableObjectsInBucketTopLevelAsync(bucket.Name, User.GetUserRoles()))
            .Select(file => new AllFilesInBucketItem
            {
                Id = file.Id,
                FileName = file.CustomName ?? file.Name,
                DateTime = file.UpdatedDate,
                Size = file.Size ?? 0,
                Type = file.Type,
                IsReading = allUserReadings.Any(reading => reading.FileId == file.Id),
                IsParted = file.NextPartId.HasValue
            }).OrderBy(f => prop?.GetValue(f, null) ?? f.FileName);

        return View(new AllFilesInBucketViewModel
        {
            Id = bucket.Id,
            BucketName = bucket.CustomName ?? bucket.Name,
            IsBelongsToUser = bucket.UserId == userGuid,
            Items = res
        });
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
                BucketId = reading.File!.BucketId,
                CustomName = reading.File?.Bucket?.CustomName ?? reading.File?.Bucket?.Name!
            })
            .ToDictionary(reading => reading.Key, reading => reading
                .OrderByDescending(f => f.UpdatedDate)
                .Select(r => new AllFilesReadingItem
                {
                    FileId = r.FileId,
                    CustomName = r.File?.CustomName ?? r.File?.Name ?? "",
                    DateTime = r.UpdatedDate,
                    Size = r.File?.Size ?? 0,
                    Page = r.Page
                }));

        return View(new AllFilesReadingViewModel { Items = res });
    }

    public async Task<IActionResult> GetFile(Guid bucketId, Guid fileId)
    {
        var file = await fileRepository.FirstOrDefaultAsync(f =>
            f.BucketId == bucketId &&
            f.Id == fileId &&
            f.AccessRoles.Intersect(User.GetUserRoles()).Any());

        if (file == null) return RedirectToAction("CustomNotFound", "Account");

        var userGuid = User.GetUserGuid();

        var reading = await readingRepository
            .FirstOrDefaultAsync(f => f.UserId == userGuid
                                      && f.File!.BucketId == bucketId
                                      && f.FileId == fileId);

        var res = new FileViewModel
        {
            UserId = userGuid,
            FileId = file.Id,
            Page = reading?.Page ?? 1,
            Scale = reading?.Scale ?? 100,
            Title = file.CustomName ?? file.Name,
            BucketId = file.BucketId,
            FileName = file.CustomName ?? file.Name
        };

        switch (file.Type)
        {
            case FileType.Pdf:
                return View("GetFilePdf", res);
            case FileType.Fb2:
                return View("GetFileFb2", res);
            case FileType.Txt:
            default:
                return RedirectToAction("CustomNotFound", "Account");
        }
    }

    [HttpGet]
    public async Task<IActionResult> UploadFile(Guid bucketId)
    {
        var bucket = await bucketRepository
            .FirstOrDefaultAsync(f => f.IsAvailable &&
                                      !f.IsHidden &&
                                      f.Id == bucketId &&
                                      f.AccessRoles.Intersect(User.GetUserRoles()).Any() &&
                                      (f.UserId == User.GetUserGuid() || f.UserId == null));

        if (bucket == null) return RedirectToAction("CustomNotFound", "Account");

        var parts = (await fileRepository.GetAllAvailableObjectsInBucketAsync(bucket.Name, User.GetUserRoles()))
            .Select(f => new SelectListItem
            {
                Text = f.CustomName ?? f.Name,
                Value = f.Id.ToString()
            })
            .ToList();

        parts.Add(new SelectListItem
        {
            Selected = true,
            Text = "Unselected",
            Value = null
        });

        return View(new UploadFileRequest { BucketId = bucketId, Parts = parts });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadFile(
        [FromForm] UploadFileRequest request)
    {
        var userRoles = User.GetUserRoles();
        var userGuid = User.GetUserGuid();

        if (userRoles.Count == 0 || userGuid == Guid.Empty) return RedirectToAction("AccessDenied", "Account");

        if (request.File == null || request.File.Length < 1)
        {
            ModelState.AddModelError(string.Empty, "File not set or empty.");
            return View(request);
        }

        const int maxFileSize = 300 * 1024 * 1024;

        if (request.File.Length > maxFileSize)
        {
            ModelState.AddModelError(string.Empty,
                $"File size is too big {GlobalFunctions.FormatSize(request.File.Length)}. Max file size is {GlobalFunctions.FormatSize(maxFileSize)}");
            return View(request);
        }

        if (string.IsNullOrEmpty(request.CustomName?.Trim()))
        {
            ModelState.AddModelError(string.Empty, "File Name not set.");
            return View(request);
        }

        if (!request.File.FileName.TryGetFileType(out var fileType))
        {
            ModelState.AddModelError(string.Empty, "File type not specified in file name or not allowed.");
            return View(request);
        }

        var bucket = await bucketRepository
            .FirstOrDefaultAsync(f => f.IsAvailable && !f.IsHidden &&
                                      f.Id == request.BucketId &&
                                      f.AccessRoles.Intersect(User.GetUserRoles()).Any() &&
                                      (f.UserId == User.GetUserGuid() || f.UserId == null));

        if (bucket == null) return RedirectToAction("CustomNotFound", "Account");

        File? asPartOfFile, asParentOfFile;

        if (request.AsPartOfId.HasValue)
        {
            asPartOfFile = await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucket.Id && !f.IsHidden && f.Id == request.AsPartOfId!.Value &&
                f.AccessRoles.Intersect(User.GetUserRoles()).Any());

            if (asPartOfFile == null)
            {
                ModelState.AddModelError(string.Empty, "Part of file not available.");
                request.AsPartOfId = null;
                return View(request);
            }
        }

        if (request.AsPartOfId.HasValue)
        {
            asParentOfFile = await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucket.Id && !f.IsHidden && f.Id == request.AsParentOfId!.Value &&
                f.AccessRoles.Intersect(User.GetUserRoles()).Any());

            if (asParentOfFile == null)
            {
                ModelState.AddModelError(string.Empty, "Parent of file not available.");
                request.AsParentOfId = null;
                return View(request);
            }
        }

        var uploadToS3Successful = await minioService.UploadObjectAsync(bucket.Name, request.File!);

        if (!uploadToS3Successful)
        {
            ModelState.AddModelError(string.Empty,
                "File upload failed. Try again later. Storage is not accessible now.");
            return View(request);
        }

        //TODO: part-parent check and set

        await fileRepository.AddAsync(new File
        {
            BucketId = bucket.Id,
            Name = request.File.FileName,
            CustomName = request.CustomName.Trim(),
            Type = fileType,
            IsAvailable = true,
            IsHidden = false,
            Size = (ulong?)request.File.Length,
            NextPartId = request.AsParentOfId
        });

        try
        {
            await fileRepository.SaveChangesAsync();
        }
        catch (Exception _)
        {
            ModelState.AddModelError(string.Empty,
                "File save failed. Try again later.");
            return View(request);
        }

        return RedirectToAction("GetAllFilesInBucket", new { bucketId = request.BucketId });
    }
}
