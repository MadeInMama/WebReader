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
    private List<MangaLibClient.ChapterInfo> AllFilesLinks = [];
    protected override string FileCustomName => "One-Punch Man";
    protected override string Url => "https://mangalib.org/ru/manga/706--onepunchman?section=chapters";
    protected override string AllFilesLinksSelector => "";
    protected override string CoverImagesSelector => "";

    protected override async Task<ImmutableList<string>> GetAllFilesLinks(CancellationToken cancellationToken)
    {
        var resp = await mangaLibClient.GetChaptersAsync(Url, cancellationToken);
        AllFilesLinks = resp;
        return resp.Select(f => f.BranchId.ToString()).ToImmutableList();
    }

    protected override Task<ImmutableList<string>> GetAllCoversLinks(CancellationToken cancellationToken)
    {
        return Task.FromResult<ImmutableList<string>>([]);
    }

    protected override async Task<Result<(bool isSkipped, File? file)>> ParseAndSaveFile(string link,
        Bucket defaultBucket,
        File? lastFile,
        string? coverLink,
        CancellationToken cancellationToken)
    {
        var chapter = AllFilesLinks.FirstOrDefault(f => f.BranchId.ToString() == link);

        if (chapter == null || (lastFile?.CurrentPartNumber ?? -1u) >= chapter.Index)
        {
            logger.LogInformation("Skipping '{fileCustomName}' '{dataNum}'", FileCustomName, chapter);
            return Result.Ok((true, lastFile));
        }

        logger.LogInformation("Go to '{link}'", link);

        var fileName =
            $"{FileCustomName.Replace(" ", "").Replace("-", "").ToLowerInvariant()}{link.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "").Trim('_')}.zip";

        var images = await mangaLibClient.GetChapterImagesBytesAsync(chapter, cancellationToken);

        var currentPartName = MyRegex().Replace(chapter.Name.Trim(), "");

        logger.LogTrace("Found '{images}' links", images.Count);

        using var memoryStream = new MemoryStream();

        using var coverMemoryStream = new MemoryStream();

        var coverFileName = $"{fileName.Replace(".zip", "")}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";

        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var el in images.OrderBy(f => f.PageIndex).Index())
            {
                var imgName =
                    $"{el.Index}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";
                var entry = zipArchive.CreateEntry(imgName, CompressionLevel.SmallestSize);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(el.Item.Bytes, cancellationToken);
                await entryStream.FlushAsync(cancellationToken);
                entryStream.Close();
            }

            if (coverLink == null) await coverMemoryStream.WriteAsync(images.First().Bytes, cancellationToken);

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
            uint.Parse(chapter.Index.ToString()),
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
