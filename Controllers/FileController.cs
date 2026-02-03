using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
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
    MinioService minioService,
    IDbContextFactory<ApplicationDbContext> contextFactory) : Controller
{
    public async Task<IActionResult> GetAllBuckets()
    {
        var res = (await bucketRepository.GetAllAvailableBucketsAsync(User.GetUserRoles(), User.GetUserGuid()))
            .Select(bucket =>
                new AllBucketsItem
                {
                    Id = bucket.Id,
                    CustomName = bucket.CustomName ?? bucket.Name,
                    DateTime = bucket.CreatedDate,
                    Size = bucket.Size ?? 0
                });

        return View(new AllBucketsViewModel { Items = res });
    }

    public async Task<IActionResult> GetAllFilesInBucket(Guid bucketId, string orderBy = "FileName")
    {
        var prop = typeof(AllFilesInBucketItem).GetProperty(orderBy);

        var userGuid = User.GetUserGuid();

        var bucket = await bucketRepository
            .FirstOrDefaultAsync(f => f.IsAvailable &&
                                      !f.IsHidden &&
                                      f.Id == bucketId &&
                                      f.AccessRoles.Intersect(User.GetUserRoles()).Any() &&
                                      (f.UserId == userGuid || f.UserId == null), null);

        if (bucket == null) return RedirectToAction("AccessDenied", "Account");

        var allUserReadings = await readingRepository.AllAsync(f => f.UserId == userGuid);

        var res = (await fileRepository.GetAllAvailableObjectsInBucketTopLevelAsync(bucket.Name, User.GetUserRoles()))
            .Select(file =>
            {
                var reading = allUserReadings.FirstOrDefault(reading => reading.FileId == file.Id);

                return new AllFilesInBucketItem
                {
                    Id = file.Id,
                    FileName = file.CustomName ?? file.Name,
                    DateTime = file.UpdatedDate,
                    Size = file.Size ?? 0,
                    Type = TypeHelper.FileTypeNameDict[file.Type],
                    IsReading = reading != null,
                    IsParted = file.NextPartId.HasValue,
                    IsDone = reading?.IsDone ?? false,
                    CurrentPartName = file.CurrentPartName
                };
            }).OrderBy(f => prop?.GetValue(f, null) ?? f.FileName);

        return View(new AllFilesInBucketViewModel
        {
            Id = bucket.Id,
            BucketName = bucket.CustomName ?? bucket.Name,
            IsBelongsToUser = bucket.UserId == userGuid,
            Items = res
        });
    }

    public async Task<IActionResult> GetAllPartsInFile(Guid bucketId, Guid fileId, string orderBy = "FileName")
    {
        var prop = typeof(AllFilesInBucketItem).GetProperty(orderBy);

        var userGuid = User.GetUserGuid();

        var contextBucket = contextFactory.CreateDbContextAsync();
        var contextFile = contextFactory.CreateDbContextAsync();

        Task.WaitAll(contextBucket, contextFile);

        var bucket = bucketRepository.FirstOrDefaultAsync(f => f.IsAvailable &&
                                                               !f.IsHidden &&
                                                               f.Id == bucketId &&
                                                               f.AccessRoles.Intersect(User.GetUserRoles())
                                                                   .Any() &&
                                                               (f.UserId == userGuid || f.UserId == null),
            contextBucket.Result);

        var file = fileRepository.FirstOrDefaultAsync(f => f.IsAvailable &&
                                                           !f.IsHidden &&
                                                           f.Id == fileId &&
                                                           f.AccessRoles.Intersect(User.GetUserRoles()).Any(),
            contextFile.Result);

        Task.WaitAll(bucket, file);

        if (bucket.Result == null || file.Result == null)
            return RedirectToAction("AccessDenied", "Account");

        var allUserReadings = await readingRepository.AllAsync(f => f.UserId == userGuid);

        var headedFile = await fileRepository.GetHeadedPartedObjectAsync(fileId);

        var files = await fileRepository.GetAllAvailableObjectsWithPartsAsync(headedFile.Id);

        var res = files.Select(f =>
        {
            var reading = allUserReadings.FirstOrDefault(reading => reading.FileId == f.Id);

            return new AllFilesInBucketItem
            {
                Id = f.Id,
                FileName = f.CustomName ?? f.Name,
                DateTime = f.UpdatedDate,
                Size = f.Size ?? 0,
                Type = TypeHelper.FileTypeNameDict[f.Type],
                IsReading = reading != null,
                IsParted = f.NextPartId.HasValue,
                IsDone = reading?.IsDone ?? false,
                CurrentPartName = f.CurrentPartName
            };
        }).OrderBy(f => prop?.GetValue(f, null) ?? f.FileName);

        return View(new AllFilesInBucketViewModel
        {
            Id = bucket.Result.Id,
            BucketName = bucket.Result.CustomName ?? bucket.Result.Name,
            IsBelongsToUser = bucket.Result.UserId == userGuid,
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
            ? readingRepository.SetCurrPageAndScaleAndIsDoneAsync(readingsToUpdate)
            : Task.CompletedTask;

        await Task.WhenAll(delete, update);

        var readings = (await readingRepository.AllAsync(f => f.UserId == userGuid &&
                                                              !f.File!.IsHidden &&
                                                              !f.IsDone &&
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
                    ReadingId = r.Id,
                    FileId = r.FileId,
                    CustomName = r.File?.CustomName ?? r.File?.Name ?? "",
                    CurrentPartName = r.File?.CurrentPartName,
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
            f.AccessRoles.Intersect(User.GetUserRoles()).Any(), null);

        if (file == null) return RedirectToAction("CustomNotFound", "Account");

        var userGuid = User.GetUserGuid();

        var reading = await readingRepository
            .FirstOrDefaultAsync(f => f.UserId == userGuid
                                      && f.File!.BucketId == bucketId
                                      && f.FileId == fileId, null);

        var res = new FileViewModel
        {
            UserId = userGuid,
            FileId = file.Id,
            Page = reading?.Page ?? 1,
            Scale = reading?.Scale ?? 100,
            Title = file.CustomName ?? file.Name,
            BucketId = file.BucketId,
            FileName = file.CustomName ?? file.Name,
            CurrentPartName = file.CurrentPartName,
            NextPartId = file.NextPartId,
            PrevPartId = (await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucketId &&
                f.NextPartId == file.Id &&
                f.AccessRoles.Intersect(User.GetUserRoles()).Any(), null))?.Id,
            Settings = file.Settings
        };

        return file.Type switch
        {
            FileType.Pdf => View("GetFilePdf", res),
            FileType.Fb2 => View("GetFileFb2", res),
            FileType.ZipWithImg => View("GetFileImg", res),
            _ => RedirectToAction("CustomNotFound", "Account")
        };
    }

    [HttpGet]
    public async Task<IActionResult> UploadFile(Guid bucketId)
    {
        var bucket = await bucketRepository
            .FirstOrDefaultAsync(f => f.IsAvailable &&
                                      !f.IsHidden &&
                                      f.Id == bucketId &&
                                      f.AccessRoles.Intersect(User.GetUserRoles()).Any() &&
                                      (f.UserId == User.GetUserGuid() || f.UserId == null), null);

        if (bucket == null) return RedirectToAction("CustomNotFound", "Account");

        var parts = GetSelectListParts(
            await fileRepository.GetAllAvailableObjectsInBucketAsync(bucket.Name, User.GetUserRoles()));

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

        var parts = GetSelectListParts(
            await fileRepository.GetAllAvailableObjectsInBucketAsync(request.BucketId, userRoles));

        request.Parts = parts;

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

        if (fileType == FileType.ZipWithImg)
        {
            var imagesCheckRes = CheckImagesInZip(request.File.OpenReadStream());

            if (!imagesCheckRes.Item1)
            {
                ModelState.AddModelError(string.Empty, imagesCheckRes.Item2!);
                return View(request);
            }
        }

        if (request is { AsParentOfId: not null, AsPartOfId: not null } &&
            request.AsParentOfId.Value == request.AsPartOfId.Value)
        {
            ModelState.AddModelError(string.Empty, "Previous and next files can't be the same.");
            return View(request);
        }

        var bucket = await bucketRepository
            .FirstOrDefaultAsync(f => f.IsAvailable && !f.IsHidden &&
                                      f.Id == request.BucketId &&
                                      f.AccessRoles.Intersect(userRoles).Any() &&
                                      (f.UserId == userGuid || f.UserId == null), null);

        if (bucket == null) return RedirectToAction("CustomNotFound", "Account");

        File? asPartOfFile = null, asParentOfFile = null;

        if (request.AsPartOfId.HasValue)
        {
            asPartOfFile = await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucket.Id && !f.IsHidden && f.Id == request.AsPartOfId!.Value &&
                f.AccessRoles.Intersect(userRoles).Any(), null);

            if (asPartOfFile == null)
            {
                ModelState.AddModelError(string.Empty, "Part of file not available.");
                request.AsPartOfId = null;
                return View(request);
            }
        }

        if (request.AsParentOfId.HasValue)
        {
            asParentOfFile = await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucket.Id && !f.IsHidden && f.Id == request.AsParentOfId!.Value &&
                f.AccessRoles.Intersect(userRoles).Any(), null);

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

        var currentFile = new File
        {
            BucketId = bucket.Id,
            Name = request.File.FileName,
            CustomName = request.CustomName.Trim(),
            Type = fileType,
            IsAvailable = true,
            IsHidden = false,
            Size = (ulong?)request.File.Length,
            NextPartId = asParentOfFile?.Id,
            CurrentPartName = request.CurrentPartName
        };

        if (asPartOfFile != null)
        {
            asPartOfFile.NextPartId = currentFile.Id;
            asPartOfFile.NextPart = currentFile;

            fileRepository.Update(asPartOfFile);
        }
        else
        {
            await fileRepository.AddAsync(currentFile);
        }

        try
        {
            await fileRepository.SaveChangesAsync();
        }
        catch (Exception _)
        {
            await minioService.RemoveObjectsAsync(bucket.Name, [currentFile.Name]);

            ModelState.AddModelError(string.Empty, "File save failed. Try again later.");
            return View(request);
        }

        return currentFile.NextPartId.HasValue
            ? RedirectToAction("GetAllPartsInFile", new { bucketId = bucket.Id, fileId = currentFile.Id })
            : RedirectToAction("GetAllFilesInBucket", new { bucketId = request.BucketId });
    }

    private static IEnumerable<SelectListItem> GetSelectListParts(IEnumerable<File> files)
    {
        var parts = files
            .OrderBy(f => f.CustomName ?? f.Name)
            .ThenBy(f => f.CurrentPartName)
            .Select(f => new SelectListItem
            {
                Text = $"{f.CustomName ?? f.Name} {f.CurrentPartName}",
                Value = f.Id.ToString()
            })
            .ToList();

        parts.Add(new SelectListItem
        {
            Selected = true,
            Text = "Unselected",
            Value = null
        });

        return parts;
    }

    private static (bool, string?) CheckImagesInZip(Stream fileStream)
    {
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
        {
            if (archive.Entries.Count == 0)
                return (false, "Files not found.");

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    return (false, "File name is null inside zip archive.");

                if (!entry.FullName.TryGetImgType(out _))
                    return (false, $"Can't get file type of file {entry.FullName}.");
            }
        }

        return (true, null);
    }
}
