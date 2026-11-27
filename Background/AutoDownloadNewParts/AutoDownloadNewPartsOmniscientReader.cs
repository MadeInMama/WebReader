using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using AngleSharp.Html.Parser;
using FluentResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
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

public class AutoDownloadNewPartsOmniscientReader(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ILogger<AutoDownloadNewPartsOmniscientReader> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository,
    ReadMangaClubClient readMangaClubClient,
    HybridCache cache)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsOmniscientReader>(
        contextFactory, logger, scheduledTaskHubContext, taskRepository, cache)
{
    private string? _html;
    protected override string FileCustomName => "Всеведущий читатель";
    protected override string Url => "https://a.zazaza.me/vseveduchii_chitatel__A5664";
    protected override string AllFilesLinksSelector => ".chapters > table .item-title > a";

    protected override string CoverImagesSelector =>
        ".cr-hero > .cr-hero-container > .cr-hero-poster-wrapper> .main-carousel > .main-carousel__content .cr-hero-poster__img";

    public override async Task PreExecuteAsync(CancellationToken cancellationToken)
    {
        _html = await readMangaClubClient.GetPageHtmlAsync(Url, cancellationToken);
    }

    protected override async Task<ImmutableList<string>> GetAllFilesLinks(CancellationToken cancellationToken)
    {
        return (await new HtmlParser().ParseDocumentAsync(_html, cancellationToken))
            .QuerySelectorAll(AllFilesLinksSelector)
            .Select(f => f.GetAttribute("href"))
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct()
            .Select(f =>
            {
                var fullUri = new Uri(new Uri(Url), f!);
                var segments = fullUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                var hasChapter = int.TryParse(segments.LastOrDefault(), out var chapterNum);

                var volSegment = segments.Length >= 2 ? segments[^2] : null;
                int.TryParse(volSegment?.Replace("vol", "", StringComparison.OrdinalIgnoreCase), out var volumeNum);

                return new
                {
                    Url = fullUri.ToString(), HasChapter = hasChapter, Volume = volumeNum, Chapter = chapterNum
                };
            })
            .Where(f => f.HasChapter)
            .OrderBy(f => f.Volume)
            .ThenBy(f => f.Chapter)
            .Select(f => f.Url)
            .ToImmutableList();
    }

    protected override async Task<ImmutableList<string>> GetAllCoversLinks(CancellationToken cancellationToken)
    {
        return (await new HtmlParser().ParseDocumentAsync(_html, cancellationToken))
            .QuerySelectorAll(CoverImagesSelector)
            .Select(f => f.GetAttribute("src") ?? f.GetAttribute("data-src")!)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct()
            .ToImmutableList();
    }

    protected override async Task<Result<(bool isSkipped, File? file)>> ParseAndSaveFile(string link,
        Bucket defaultBucket,
        File? lastFile,
        string? coverLink,
        CancellationToken cancellationToken)
    {
        var dataNum = link.Split("/").Last() == "0" ? 0 : int.Parse(link.Split("/").Last());

        if ((lastFile?.CurrentPartNumber ?? -1u) >= dataNum)
        {
            logger.LogInformation("Skipping '{fileCustomName}' '{dataNum}'", FileCustomName, dataNum);
            return Result.Ok((true, lastFile));
        }

        logger.LogInformation("Go to '{link}'", link);

        var fileName =
            $"{link.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "").Trim('_')}.zip";

        var html = await readMangaClubClient.GetChapterHtmlAsync(link, cancellationToken);

        var htmlDocument = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);

        var images = ParseChapterImagesFromScript(html);

        var currentPartName = MyRegex().Replace(
            htmlDocument.QuerySelector(".text-cut")?.TextContent.Trim() ?? dataNum.ToString(), "");

        logger.LogTrace("Found '{images}' links", images.Count);

        using var memoryStream = new MemoryStream();

        using var coverMemoryStream = new MemoryStream();

        var coverFileName = $"{fileName.Replace(".zip", "")}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";

        if (coverLink != null)
            await coverMemoryStream.WriteAsync(
                await readMangaClubClient.GetImageByteArrayAsync(coverLink, cancellationToken), cancellationToken);

        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var imagesBytes = new List<byte[]>();

            foreach (var img in images)
            {
                var imageBytes = await readMangaClubClient.GetImageByteArrayAsync(img, cancellationToken);

                imagesBytes.Add(imageBytes);
            }

            var splitImages = ImageSplitter.Split(ImageMerger.MergeByWidth(imagesBytes, logger), logger);

            foreach (var el in splitImages.Index())
            {
                var imgName =
                    $"{el.Index}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";
                var entry = zipArchive.CreateEntry(imgName, CompressionLevel.SmallestSize);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(el.Item, cancellationToken);
                await entryStream.FlushAsync(cancellationToken);
                entryStream.Close();
            }

            if (coverLink == null) await coverMemoryStream.WriteAsync(splitImages.First(), cancellationToken);

            imagesBytes.Clear();
            imagesBytes.TrimExcess();
            imagesBytes = null;
            splitImages.Clear();
            splitImages.TrimExcess();
            splitImages = null;
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
            uint.Parse(dataNum.ToString()),
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
