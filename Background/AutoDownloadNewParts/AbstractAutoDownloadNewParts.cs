using System.Collections.Immutable;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using FluentResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models;
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
    MinioService minioService,
    ITelegramBotClient botClient,
    IHttpClientFactory httpClientFactory,
    ILogger<T> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository)
    : AbstractBackgroundTasked<T>(taskRepository, scheduledTaskHubContext, logger)
{
    private const ulong DefaultMaxSize = 1024u; //1gb
    private const string SettingSizeName = "max_size";

    private readonly BrowserFetcher _browserFetcher = new();

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

            var browser = await GetBrowser(cancellationToken);

            var page = await GetNewPage(browser);

            logger.LogTrace("Go to {url}", Url);
            await page.GoToAsync(Url,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 30000 });

            logger.LogTrace("Waiting for page load");
            await page.WaitForSelectorAsync(".chapters", new WaitForSelectorOptions { Timeout = 30000 });

            var html = await page.GetContentAsync();

            await CloseBrowser(browser);

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

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (result.IsSuccess)
                result = Result.Ok($"Downloaded count: {links.Count}");

            // UninstallBrowsers();
        }
        catch (TimeoutException e)
        {
            result = Result.Fail($"Timeout has been reached: {e.Message}");

            // UninstallBrowsers();
        }
        catch (NavigationException e)
        {
            result = Result.Fail($"Navigation error has been reached: {e.Message}");

            // UninstallBrowsers();
        }
        catch (HttpRequestException e)
        {
            result = Result.Fail($"HttpRequest error has been reached: {e.Message}");

            // UninstallBrowsers();
        }
        finally
        {
            BrowserProcessKiller.PrepareCleanBrowserEnvironment(logger);
            SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return result;
    }

    protected virtual async Task<ImmutableList<KeyValuePair<string, IElement>>> GetAllFilesLinks(string html,
        string selector, CancellationToken cancellationToken)
    {
        return (await new HtmlParser().ParseDocumentAsync(html, cancellationToken))
            .QuerySelectorAll(selector)
            .Where(f => f.GetAttribute("href") != null)
            .ToDictionary(f => f.GetAttribute("href")!, f => f)
            .Where(f => !f.Key.IsNullOrEmptyOrWhitespace())
            .Where(f => int.TryParse(f.Key.Split("/").Last(), out _))
            .OrderBy(f => int.Parse(f.Key.Split("/").Last()))
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

        if (settings.RootElement.GetProperty(SettingSizeName).TryGetUInt64(out var res))
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

    protected virtual async Task<IBrowser> GetBrowser(CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            logger.LogTrace("Detected Windows");
            _browserFetcher.Browser = SupportedBrowser.Chrome;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.LogTrace("Detected Linux");
            _browserFetcher.Browser = SupportedBrowser.Chromium;
        }

        logger.LogTrace("Detected installed browsers: {join}",
            string.Join(", ", _browserFetcher.GetInstalledBrowsers().Select(f => f.GetExecutablePath())));

        //UninstallBrowsers();

        if (!_browserFetcher.GetInstalledBrowsers().Any())
        {
            logger.LogTrace("Download Browser");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                await _browserFetcher.DownloadAsync(BrowserTag.Stable);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                await _browserFetcher.DownloadAsync(BrowserTag.Latest);

            logger.LogTrace("Detected installed browsers: {join}",
                string.Join(", ", _browserFetcher.GetInstalledBrowsers().Select(f => f.GetExecutablePath())));
        }

        var browserTask = Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--lang=en-US",
                "--disable-image-loading",
                "--disable-notifications",
                "--use-gl=swiftshader",
                "--enable-low-end-device-mode",
                "--enable-features=InfiniteTabsFreezing",
                "--disable-dev-shm-usage", // Overcome /dev/shm space limitations (critical for Docker/low RAM)
                "--disable-background-networking", // Disable background services
                "--disable-default-apps",
                "--disable-extensions",
                "--disable-sync",
                "--disable-translate",
                "--hide-scrollbars",
                "--metrics-recording-only",
                "--mute-audio",
                "--no-first-run",
                "--safebrowsing-disable-auto-update",
                "--memory-pressure-off", // Tells Chrome to ignore memory pressure signals
                "--no-zygote",
                "--disable-background-timer-throttling",
                "--disable-backgrounding-occluded-windows",
                "--disable-software-rasterizer"
            ],
            ExecutablePath =
                _browserFetcher.GetExecutablePath(_browserFetcher.GetInstalledBrowsers().First().BuildId)
        });

        var browser = await browserTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        return browser;
    }

    protected virtual void UninstallBrowsers()
    {
        foreach (var el in _browserFetcher.GetInstalledBrowsers())
            _browserFetcher.CustomUninstall(el.Browser, el.Platform, el.BuildId, logger);
    }

    protected virtual async Task<IPage> GetNewPage(IBrowser browser)
    {
        var res = await browser.NewPageAsync();
        await res.SetRequestInterceptionAsync(true);

        res.Request += async (_, e) =>
        {
            var resourceType = e.Request.ResourceType;

            if (new[]
                {
                    ResourceType.Image,
                    ResourceType.ImageSet,
                    ResourceType.Img,
                    ResourceType.StyleSheet,
                    ResourceType.Font,
                    ResourceType.Media,
                    ResourceType.WebSocket
                }.Contains(resourceType))
                await e.Request.AbortAsync();
            else
                await e.Request.ContinueAsync();
        };

        await res.SetExtraHttpHeadersAsync(new Dictionary<string, string>
        {
            { "Accept-Language", "ru-RU,ru;q=0.9" },
            { "Referer", "https://readmanga.ru/" }
        });

        return res;
    }

    protected virtual async Task BroadcastAllSubs(ApplicationDbContext context, ITelegramBotClient botClient,
        string message)
    {
        await foreach (var el in context.SubscriberTgs.AsAsyncEnumerable())
            try
            {
                await botClient.SendMessage(el.ChatId, message);
                await Task.Delay(30);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 403)
            {
                var subscriberTg = await context.SubscriberTgs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ChatId == el.ChatId);

                if (subscriberTg != null)
                {
                    context.SubscriberTgs.Remove(subscriberTg);
                    await context.SaveChangesAsync();
                }
            }
    }

    protected virtual async Task CloseBrowser(IBrowser browser)
    {
        await browser.CloseAsync();
        await browser.DisposeAsync();
        BrowserProcessKiller.PrepareCleanBrowserEnvironment(logger);
    }

    protected virtual async Task<Result<(bool isSkipped, File? file)>> ParseAndSaveFile(
        KeyValuePair<string, IElement> link,
        Bucket defaultBucket,
        File? lastFile,
        CancellationToken cancellationToken)
    {
        var dataNum = link.Key.Split("/").Last() == "0" ? 0 : int.Parse(link.Key.Split("/").Last());

        if ((lastFile?.CurrentPartNumber ?? -1u) >= dataNum)
        {
            logger.LogTrace("Skipping {fileCustomName} {dataNum}", FileCustomName, dataNum);
            return Result.Ok((true, lastFile));
        }

        logger.LogTrace("Go to {link}", link);

        var fileName =
            $"{link.Key.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "").Trim('_')}.zip";
        var currentPartName = MyRegex().Replace(string.Concat(link.Value.ChildNodes
            .Where(node => node.NodeType == NodeType.Text)
            .Select(node => node.TextContent)).Trim(), "");

        var browser = await GetBrowser(cancellationToken);

        var page = await GetNewPage(browser);

        await page.GoToAsync(link.Key,
            new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 30000 });

        logger.LogTrace("Waiting for page load with images");
        await page.WaitForSelectorAsync("#fotocontext", new WaitForSelectorOptions { Timeout = 30000 });

        var html = await page.GetContentAsync();

        await CloseBrowser(browser);

        var images = (await new HtmlParser().ParseDocumentAsync(html, cancellationToken))
            .QuerySelectorAll("#fotocontext > .manga-img-placeholder > img")
            .ToImmutableList();

        logger.LogTrace("Found {images} links", images.Count);

        using var memoryStream = new MemoryStream();

        using var httpClient = httpClientFactory.CreateClient("parser-http-client");

        var isCoverDone = false;
        var coverFileName = lastFile?.CoverName;

        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var img in images.OrderBy(f => int.Parse(f.GetAttribute("data-page"))))
            {
                var src = img.GetAttribute("data-original");
                if (src.IsNullOrEmptyOrWhitespace())
                    src = img.GetAttribute("src")!;

                var imageBytes = await httpClient.GetByteArrayAsync(src, cancellationToken);

                if (ImageEmptyChecker.IsEmpty(imageBytes)) continue;

                var splitImages = ImageSplitter.SplitImage(StaticFunctions.ConvertByteArrayToJpeg(imageBytes));

                foreach (var el in splitImages.Index())
                {
                    var imgName =
                        $"{img.GetAttribute("data-page")}-{el.Index}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";
                    var entry = zipArchive.CreateEntry(imgName, CompressionLevel.SmallestSize);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(el.Item, cancellationToken);
                    await entryStream.FlushAsync(cancellationToken);
                    entryStream.Close();
                }

                if (!isCoverDone)
                {
                    var cover = splitImages.PickRandom();

                    if (cover is { Length: > 0 })
                    {
                        using var coverMemoryStream = new MemoryStream(cover);
                        var expectedCoverFileName =
                            $"{fileName.Replace(".zip", "")}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";
                        var isSuccessful = await minioService.UploadCoverAsync(coverMemoryStream, expectedCoverFileName,
                            "image/jpeg", cancellationToken);

                        if (isSuccessful)
                        {
                            coverFileName = expectedCoverFileName;
                            isCoverDone = true;
                        }
                    }
                }

                SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            }
        }

        await memoryStream.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        logger.LogTrace("Save to db {name}", fileName);

        var fileUploadResult = await fileUploadService.UploadFileAsync(
            lastFile?.Id,
            null,
            lastFile?.Bucket ?? defaultBucket,
            Enum.GetValues<RoleType>().ToList(),
            memoryStream,
            fileName,
            FileCustomName,
            "application/zip",
            FileType.ZipWithImg,
            currentPartName,
            uint.Parse(dataNum.ToString()),
            coverFileName,
            cancellationToken
        );

        memoryStream.Close();

        if (fileUploadResult.IsFailed)
            return Result.Fail(new Error($"Error saving '{fileName}' to S3"));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // await BroadcastAllSubs(context, botClient,
        // $"File {FileCustomName} {currentPartName} has been uploaded automatically.");

        var currentSize = await CurrentSize(context, FileCustomName, cancellationToken);

        if (CheckMaxSizeReached(MaxSize, currentSize, FileCustomName))
            return Result.Fail(
                $"Max size {GlobalFunctions.FormatSize(MaxSize)} of '{FileCustomName}' has been reached {GlobalFunctions.FormatSize(currentSize)}");

        GC.Collect();
        GC.WaitForPendingFinalizers();

        return Result.Ok((false, fileUploadResult.Value))!;
    }

    [GeneratedRegex(@"^\d+\s*-\s*")]
    private static partial Regex MyRegex();
}
