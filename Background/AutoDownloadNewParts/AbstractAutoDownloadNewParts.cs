using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
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
    NotificationRepository notificationRepository)
    : AbstractBackgroundTasked<T>(taskRepository, scheduledTaskHubContext, logger)
{
    private const ulong DefaultMaxSize = 1024u; //1gb

    protected abstract string FileCustomName { get; }
    protected abstract string Url { get; }
    protected abstract string AllFilesLinksSelector { get; }
    protected abstract string CoverImagesSelector { get; }
    protected virtual ulong MaxSize { get; set; }

    protected abstract Task<ImmutableList<string>> GetAllFilesLinks(CancellationToken cancellationToken);
    protected abstract Task<ImmutableList<string>> GetAllCoversLinks(CancellationToken cancellationToken);

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

            var links = await GetAllFilesLinks(cancellationToken);

            var coverLinks = await GetAllCoversLinks(cancellationToken);

            logger.LogTrace("Found '{links}' links", links.Count);

            var defaultBucket = await GetDefaultBucket(context, cancellationToken);

            var lastFile = await GetLastStoredFile(context, cancellationToken);

            foreach (var link in links.Index())
            {
                var res = await ParseAndSaveFile(link.Item, defaultBucket, lastFile, coverLinks.PickRandom(),
                    cancellationToken);

                if (res.IsFailed)
                {
                    logger.LogTrace("'{msg}'", res.ToString());

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

    protected virtual async Task<Bucket> GetDefaultBucket(ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.Buckets.AsNoTracking().FirstAsync(b => b.Name == "mybucket", cancellationToken);
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
            logger.LogTrace("Current '{fileCustomName}' size '{currentSize}' of '{maxSize}'",
                fileCustomName,
                GlobalFunctions.FormatSize(currentSize),
                GlobalFunctions.FormatSize(maxSize));
            return false;
        }

        logger.LogTrace("Max size '{maxSize}' of '{fileCustomName}' has been reached '{currentSize}'",
            GlobalFunctions.FormatSize(maxSize),
            fileCustomName,
            GlobalFunctions.FormatSize(currentSize));

        return true;
    }

    protected ImmutableList<string> ParseChapterImagesFromScript(string html)
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

    protected static int ExtractPageNumber(string url)
    {
        var fileName = Path.GetFileNameWithoutExtension(url);

        var numberMatch = Regex.Match(fileName, @"\d+");

        return numberMatch.Success && int.TryParse(numberMatch.Value, out var pageNumber)
            ? pageNumber
            : int.MaxValue;
    }

    protected abstract Task<Result<(bool isSkipped, File? file)>> ParseAndSaveFile(string link,
        Bucket defaultBucket,
        File? lastFile,
        string? coverLink,
        CancellationToken cancellationToken);

    [GeneratedRegex(@"^\d+\s*-\s*")]
    protected static partial Regex MyRegex();
}
