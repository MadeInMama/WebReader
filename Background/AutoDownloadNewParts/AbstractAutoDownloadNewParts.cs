using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
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
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.AutoDownloadNewParts;

//TODO: refactor
public abstract partial class AbstractAutoDownloadNewParts<T>(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ILogger<T> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository,
    ReadMangaClubClient readMangaClubClient)
    : AbstractBackgroundTasked<T>(taskRepository, scheduledTaskHubContext, logger)
{
    private const ulong DefaultMaxSize = 1024u; //1gb

    protected abstract string FileCustomName { get; }
    protected abstract string Url { get; }
    protected virtual ulong MaxSize { get; set; }

    public override async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var result = Result.Ok("");

        try
        {
            MaxSize = GetMaxSize(task.Settings);

            var currentSize = await CurrentSize(context, FileCustomName, cancellationToken);

            if (CheckMaxSizeReached(MaxSize, currentSize, FileCustomName))
                return Result.Fail(
                    $"Max size {GlobalFunctions.FormatSize(MaxSize)} of '{FileCustomName}' has been reached {GlobalFunctions.FormatSize(currentSize)}");

            var html = await readMangaClubClient.GetPageHtmlAsync(Url, cancellationToken);

            var links = await GetAllFilesLinks(html, ".chapters > table .item-title > a", cancellationToken);

            logger.LogTrace("Found {links} links", links.Count);

            var defaultBucket = await GetDefaultBucket(context, cancellationToken);

            var lastFile = await GetLastStoredFile(context, cancellationToken);

            foreach (var link in links.Index())
            {
                var res = await ParseAndSaveFile(link.Item, defaultBucket, lastFile, cancellationToken);

                if (res.IsFailed)
                {
                    logger.LogTrace("{msg}", res.ToString());

                    result = Result.Fail(string.Join(", ", res.Reasons.Select(f => f.Message)));

                    break;
                }

                await UpdateProgress(task.Id, TaskStatus.InProgress, new decimal(link.Index) / links.Count, null,
                    cancellationToken);

                lastFile = res.ValueOrDefault.file;
            }

            if (result.IsSuccess)
                result = Result.Ok($"Downloaded count: {links.Count}");
        }
        catch (HttpRequestException e)
        {
            result = Result.Fail($"HttpRequest error has been reached: {e.Message}");
        }
        finally
        {
            SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        return result;
    }

    protected virtual async Task<ImmutableList<string>> GetAllFilesLinks(string html,
        string selector, CancellationToken cancellationToken)
    {
        return (await new HtmlParser().ParseDocumentAsync(html, cancellationToken))
            .QuerySelectorAll(selector)
            .Select(el => el.GetAttribute("href"))
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Distinct()
            .Select(href =>
            {
                var fullUri = new Uri(new Uri(Url), href!);
                var segments = fullUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                var hasChapter = int.TryParse(segments.LastOrDefault(), out var chapterNum);

                var volSegment = segments.Length >= 2 ? segments[^2] : null;
                int.TryParse(volSegment?.Replace("vol", "", StringComparison.OrdinalIgnoreCase), out var volumeNum);

                return new
                {
                    Url = fullUri.ToString(), HasChapter = hasChapter, Volume = volumeNum, Chapter = chapterNum
                };
            })
            .Where(x => x.HasChapter)
            .OrderBy(x => x.Volume)
            .ThenBy(x => x.Chapter)
            .Select(x => x.Url)
            .ToImmutableList();
    }

    protected virtual async Task<Bucket> GetDefaultBucket(ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.Buckets.FirstAsync(b => b.Name == "mybucket", cancellationToken);
    }

    protected virtual async Task<File?> GetLastStoredFile(ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.Files.Where(f => f.CustomName == FileCustomName)
            .Include(f => f.Bucket)
            .AsAsyncEnumerable()
            .OrderBy(f => f.CurrentPartNumber!)
            .LastOrDefaultAsync(cancellationToken);
    }

    protected virtual ulong GetMaxSize(JsonDocument settings)
    {
        const uint toByteMultiplier = 1024u * 1024u;

        if (settings.RootElement.GetProperty(StaticJsonNames.MaxSize).TryGetUInt64(out var res))
            return res * toByteMultiplier;

        return DefaultMaxSize * toByteMultiplier;
    }

    protected virtual async Task<ulong> CurrentSize(ApplicationDbContext context, string fileCustomName,
        CancellationToken cancellationToken)
    {
        return await context.Files.Where(f => f.CustomName == fileCustomName)
            .AsNoTracking()
            .Select(f => f.Size)
            .AsAsyncEnumerable()
            .AggregateAsync(0ul, (currentSum, nullableValue) => currentSum + (nullableValue ?? 0ul), cancellationToken);
    }

    protected virtual bool CheckMaxSizeReached(ulong maxSize, ulong currentSize, string fileCustomName)
    {
        if (maxSize >= currentSize)
        {
            logger.LogTrace("Current '{fileCustomName}' size {currentSize} of {maxSize}",
                fileCustomName,
                GlobalFunctions.FormatSize(currentSize),
                GlobalFunctions.FormatSize(maxSize));
            return false;
        }

        logger.LogTrace("Max size {maxSize} of '{fileCustomName}' has been reached {currentSize}",
            GlobalFunctions.FormatSize(maxSize),
            fileCustomName,
            GlobalFunctions.FormatSize(currentSize));

        return true;
    }

    private ImmutableList<string> ParseChapterImagesFromScript(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return ImmutableList<string>.Empty;

        var match = Regex.Match(html, @"rm_h\.readerInit\s*\(\s*\d+\s*,\s*(\[\[.*?\]\])", RegexOptions.Singleline);

        if (!match.Success)
            match = Regex.Match(html, @"(\[\[\s*['""]https?://.*?['""]\s*,.*?\.(?:jpg|jpeg|png|webp).*?\]\])",
                RegexOptions.Singleline);

        if (!match.Success)
            return ImmutableList<string>.Empty;

        var jsonArray = match.Groups[1].Value.Replace('\'', '"');

        try
        {
            using var doc = JsonDocument.Parse(jsonArray);
            var urls = ImmutableList.CreateBuilder<string>();

            foreach (var item in doc.RootElement.EnumerateArray())
                if (item.GetArrayLength() >= 3)
                {
                    var server = item[0].GetString() ?? "";
                    var prefix = item[1].GetString() ?? "";
                    var path = item[2].GetString() ?? "";

                    var rawUrl = $"{server}{prefix}{path}";

                    if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var validUri))
                    {
                        var cleanUrl = validUri.GetLeftPart(UriPartial.Path);
                        urls.Add(cleanUrl);
                    }
                }

            return urls
                .OrderBy(ExtractPageNumber)
                .ThenBy(url => url)
                .ToImmutableList();
        }
        catch
        {
            return ImmutableList<string>.Empty;
        }
    }

    private static int ExtractPageNumber(string url)
    {
        var fileName = Path.GetFileNameWithoutExtension(url);

        var numberMatch = Regex.Match(fileName, @"\d+");

        return numberMatch.Success && int.TryParse(numberMatch.Value, out var pageNumber)
            ? pageNumber
            : int.MaxValue;
    }

    protected virtual async Task<Result<(bool isSkipped, File? file)>> ParseAndSaveFile(
        string link,
        Bucket defaultBucket,
        File? lastFile,
        CancellationToken cancellationToken)
    {
        var dataNum = link.Split("/").Last() == "0" ? 0 : int.Parse(link.Split("/").Last());

        if ((lastFile?.CurrentPartNumber ?? -1u) >= dataNum)
        {
            logger.LogInformation("Skipping {fileCustomName} {dataNum}", FileCustomName, dataNum);
            return Result.Ok((true, lastFile));
        }

        logger.LogInformation("Go to {link}", link);

        var fileName =
            $"{link.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "").Trim('_')}.zip";

        var html = await readMangaClubClient.GetChapterHtmlAsync(link, cancellationToken);

        var htmlDocument = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);

        var images = ParseChapterImagesFromScript(html);

        var currentPartName = MyRegex().Replace(
            htmlDocument.QuerySelector(".text-cut")?.TextContent.Split(" – ").Last().Trim() ??
            dataNum.ToString(), "");

        logger.LogTrace("Found {images} links", images.Count);

        using var memoryStream = new MemoryStream();

        using var coverMemoryStream = new MemoryStream();

        var coverFileName = lastFile?.CoverName;

        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var imagesBytes = new List<byte[]>();

            foreach (var img in images)
            {
                var imageBytes = await readMangaClubClient.GetImageByteArrayAsync(img, cancellationToken);

                imagesBytes.Add(imageBytes);
            }

            var splitImages = ImageSplitter.Split(ImageMerger.Merge(imagesBytes, logger), logger);

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

            var cover = splitImages.First();

            if (cover is { Length: > 0 })
            {
                await coverMemoryStream.WriteAsync(cover, cancellationToken);
                coverFileName = $"{fileName.Replace(".zip", "")}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";
            }

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

        logger.LogTrace("Save to db {name}", fileName);

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

    [GeneratedRegex(@"^\d+\s*-\s*")]
    private static partial Regex MyRegex();
}
