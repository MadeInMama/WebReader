using System.IO.Compression;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Repositories;
using WebReader.Services;
using File = WebReader.Models.Entities.File;

namespace WebReader.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public class FileController(
    BucketRepository bucketRepository,
    FileRepository fileRepository,
    FileUploadService fileUploadService,
    FileControllerService fileControllerService) : Controller
{
    public async Task<IActionResult> GetAllBuckets()
    {
        return View((await fileControllerService.GetAllBuckets(User.GetUserGuid(), User.GetUserRoles())).Value);
    }

    public async Task<IActionResult> GetAllFilesInBucket(Guid bucketId, string orderBy = "FileName")
    {
        var res = await fileControllerService.GetAllFilesInBucket(User.GetUserGuid(), User.GetUserRoles(), bucketId,
            orderBy);

        if (res.IsFailed) return RedirectToAction("AccessDenied", "Account");

        return View(res.Value);
    }

    public async Task<IActionResult> GetAllPartsInFile(Guid bucketId, Guid fileId, string orderBy = "FileName")
    {
        var res = await fileControllerService.GetAllPartsInFile(User.GetUserGuid(), User.GetUserRoles(), bucketId,
            fileId, orderBy);

        if (res.IsFailed) return RedirectToAction("AccessDenied", "Account");

        return View(res.Value);
    }

    public async Task<IActionResult> GetReading()
    {
        var res = await fileControllerService.GetReading(User.GetUserGuid(), User.GetUserRoles());

        return View(res.Value);
    }

    public async Task<IActionResult> GetFile(Guid bucketId, Guid fileId)
    {
        var res = await fileControllerService.GetFile(User.GetUserGuid(), User.GetUserRoles(), bucketId, fileId);

        if (res.IsFailed) return RedirectToAction("CustomNotFound", "Account");

        return res.Value.Type switch

        {
            FileType.Pdf => View("GetFilePdf", res.Value),
            FileType.Fb2 => View("GetFileFb2", res.Value),
            FileType.ZipWithImg => View("GetFileImg", res.Value),
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
                                      (f.UserId == User.GetUserGuid() || f.UserId == null), null, true);

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

            if (imagesCheckRes.IsFailed)
            {
                ModelState.AddModelError(string.Empty, imagesCheckRes.ToString());
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
                                      (f.UserId == userGuid || f.UserId == null), null, true);

        if (bucket == null) return RedirectToAction("CustomNotFound", "Account");

        var uploadFileResult = await fileUploadService.UploadFileAsync(
            request.AsPartOfId,
            request.AsParentOfId,
            bucket,
            userRoles,
            request.File.OpenReadStream(),
            request.File.FileName,
            request.CustomName,
            request.File.ContentType,
            fileType,
            request.CurrentPartName,
            request.CurrentPartNumber);

        if (uploadFileResult.IsFailed)
        {
            uploadFileResult.HasError<Error>(out var errorMessage);
            return View(string.Empty, errorMessage.FirstOrDefault()?.Message);
        }

        return uploadFileResult.Value is { NextPartId: not null }
            ? RedirectToAction("GetAllPartsInFile",
                new { bucketId = bucket.Id, fileId = uploadFileResult.Value.Id })
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

    private static Result CheckImagesInZip(Stream fileStream)
    {
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
        {
            if (archive.Entries.Count == 0)
                return Result.Fail("Files not found.");

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    return Result.Fail("File name is null inside zip archive.");

                if (!entry.FullName.TryGetImgType(out _))
                    return Result.Fail($"Can't get file type of file {entry.FullName}.");
            }
        }

        return Result.Ok();
    }
}
