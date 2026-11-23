using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using WebReader.Services.Http;
using File = WebReader.Models.Entities.File;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsOnePunchMan(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ILogger<AutoDownloadNewPartsOnePunchMan> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository,
    MangaLibClient mangaLibClient)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsOnePunchMan>(
        contextFactory, fileUploadService, logger, scheduledTaskHubContext,
        taskRepository, notificationRepository)
{
    private List<MangaLibChapterDto> AllFilesLinks = [];
    protected override string FileCustomName => "One-Punch Man";
    protected override string Url => "706--onepunchman";
    protected override string AllFilesLinksSelector => ".vue-recycle-scroller__item-view a";
    protected override string CoverImagesSelector => ".cover__img";

    protected override async Task<ImmutableList<string>> GetAllFilesLinks(CancellationToken cancellationToken)
    {
        var resp = await mangaLibClient.GetChaptersAsync(Url, cancellationToken);
        AllFilesLinks = resp;
        return resp.Select(f => f.Url).ToImmutableList();
    }

    protected override async Task<ImmutableList<string>> GetAllCoversLinks(CancellationToken cancellationToken)
    {
        var resp = await mangaLibClient.GetAllCoversAsync(Url, cancellationToken);
        return resp.Select(f => f.FullImageUrl).ToImmutableList();
    }

    protected override async Task<Result<(bool isSkipped, File? file)>> ParseAndSaveFile(string link,
        Bucket defaultBucket,
        File? lastFile,
        string? coverLink,
        CancellationToken cancellationToken)
    {
        var dataNum = AllFilesLinks.FirstOrDefault(f => f.Url == link);

        if (dataNum == null || (lastFile?.CurrentPartNumber ?? -1u) >= dataNum.Index)
        {
            logger.LogInformation("Skipping '{fileCustomName}' '{dataNum}'", FileCustomName, dataNum);
            return Result.Ok((true, lastFile));
        }

        logger.LogInformation("Go to '{link}'", link);

        var fileName =
            $"{link.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "").Trim('_')}.zip";

        var images = await mangaLibClient.GetAllImagesBytesAsync(link, 1, cancellationToken);

        var currentPartName = MyRegex().Replace((dataNum.Name ?? dataNum.Number).Trim(), "");

        logger.LogTrace("Found '{images}' links", images.Count);

        using var memoryStream = new MemoryStream();

        using var coverMemoryStream = new MemoryStream();

        var coverFileName = $"{fileName.Replace(".zip", "")}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";

        if (coverLink != null)
            await coverMemoryStream.WriteAsync(
                await mangaLibClient.DownloadImageBytesAsync(coverLink, cancellationToken), cancellationToken);

        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var el in images.Index())
            {
                var imgName =
                    $"{el.Index}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";
                var entry = zipArchive.CreateEntry(imgName, CompressionLevel.SmallestSize);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(el.Item, cancellationToken);
                await entryStream.FlushAsync(cancellationToken);
                entryStream.Close();
            }

            if (coverLink == null) await coverMemoryStream.WriteAsync(images.First(), cancellationToken);

            images.Clear();
            images.TrimExcess();
            images = null;
        }

        await memoryStream.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        await coverMemoryStream.FlushAsync(cancellationToken);
        coverMemoryStream.Position = 0;

        logger.LogTrace("Save to db '{name}'", fileName);

        var fileUploadResult = await fileUploadService.UploadFileAsync(
            lastFile?.Id,
            null,
            lastFile?.Bucket ?? defaultBucket,
            [.. Enum.GetValues<RoleType>()],
            memoryStream,
            fileName,
            FileCustomName,
            "application/zip",
            FileType.ZipWithImg,
            currentPartName,
            uint.Parse(dataNum.Index.ToString()),
            coverFileName,
            coverMemoryStream,
            cancellationToken
        );

        memoryStream.Close();
        coverMemoryStream.Close();

        if (fileUploadResult.IsFailed)
            return Result.Fail(new Error($"Error saving '{fileName}' to S3"));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        using (var message = JsonSerializer.SerializeToDocument(new Dictionary<string, string>
               {
                   [StaticJsonNames.NotificationBody] = $"File '{FileCustomName}' has new part '{currentPartName}'.",
                   [StaticJsonNames.NotificationBucketId] = fileUploadResult.Value.BucketId.ToString(),
                   [StaticJsonNames.NotificationFileId] = fileUploadResult.Value.Id.ToString()
               }))
        {
            await notificationRepository.AddAsync(new Models.Entities.Notification
            {
                Message = message,
                Type = NotificationType.NewFile
            }, cancellationToken);

            await notificationRepository.SaveChangesAsync(cancellationToken);
        }

        var currentSize = await CurrentSize(context, FileCustomName, cancellationToken);

        if (CheckMaxSizeReached(MaxSize, currentSize, FileCustomName))
            return Result.Fail(
                $"Max size {GlobalFunctions.FormatSize(MaxSize)} of '{FileCustomName}' has been reached {GlobalFunctions.FormatSize(currentSize)}");

        SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return Result.Ok((false, fileUploadResult.Value))!;
    }
}
