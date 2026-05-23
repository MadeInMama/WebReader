using System.Collections.Immutable;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Services;
using File = WebReader.Models.Entities.File;

namespace WebReader.Background.AutoDownloadNewParts;

public abstract partial class AbstractAutoDownloadNewParts<T>(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ITelegramBotClient botClient,
    IHttpClientFactory httpClientFactory,
    ILogger<T> logger)
    : IBackgroundTasked
{
    private const ulong DefaultMaxSize = 1024u; //1gb
    private const string SettingSizeName = "max_size";

    private readonly BrowserFetcher _browserFetcher = new();

    protected abstract string FileCustomName { get; }
    protected abstract string Url { get; }
    protected virtual ulong MaxSize { get; set; }

    public virtual async Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            MaxSize = GetMaxSize(task.ScheduledTaskConfig);

            var currentSize = await CurrentSize(context, FileCustomName, cancellationToken);

            if (CheckMaxSizeReached(MaxSize, currentSize, FileCustomName)) return;

            var browser = await GetBrowser(cancellationToken);

            var page = await GetNewPage(browser);

            logger.LogInformation("Go to {url}", Url);
            await page.GoToAsync(Url,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 30000 });

            logger.LogInformation("Waiting for page load");
            await page.WaitForSelectorAsync(".chapters", new WaitForSelectorOptions { Timeout = 30000 });

            var html = await page.GetContentAsync();

            await CloseBrowser(browser);

            var links = (await new HtmlParser().ParseDocumentAsync(html, cancellationToken))
                .QuerySelectorAll(".chapters > table .item-title > a")
                .Where(f => f.GetAttribute("href") != null)
                .ToDictionary(f => f.GetAttribute("href")!, f => f)
                .Where(f => !f.Key.IsNullOrEmptyOrWhitespace())
                .Where(f => int.TryParse(f.Key.Split("/").Last(), out _))
                .OrderBy(f => int.Parse(f.Key.Split("/").Last()))
                .ToImmutableList();

            logger.LogInformation("Found {links} links", links.Count);

            var defaultBucket = await context.Buckets.FirstAsync(b => b.Name == "mybucket", cancellationToken);

            var lastStoredFile = await context.Files.Where(f => f.CustomName == FileCustomName)
                .Include(f => f.Bucket)
                .AsAsyncEnumerable()
                .OrderBy(f => f.CurrentPartNumber!)
                .LastOrDefaultAsync(cancellationToken);

            var lastFile = lastStoredFile;

            foreach (var link in links)
            {
                var res = await ParseAndSaveFile(link, defaultBucket, lastFile, cancellationToken);

                if (res.IsFailed)
                {
                    logger.LogError("{msg}", res.ToString());

                    break;
                }

                lastFile = res.ValueOrDefault;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // UninstallBrowsers();
        }
        catch (TimeoutException e)
        {
            logger.LogError("Timeout has been reached: {}", e.Message);
            // UninstallBrowsers();
        }
        catch (NavigationException e)
        {
            logger.LogError("Navigation error has been reached: {}", e.Message);
            // UninstallBrowsers();
        }
        catch (HttpRequestException e)
        {
            logger.LogError("HttpRequest error has been reached: {}", e.Message);
            // UninstallBrowsers();
        }
        finally
        {
            BrowserProcessKiller.PrepareCleanBrowserEnvironment(logger);
            SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    protected virtual ulong GetMaxSize(ScheduledTaskConfig? config)
    {
        const uint toByteMultiplier = 1024u * 1024u;

        if (config != null && config.Settings.RootElement.GetProperty(SettingSizeName).TryGetUInt64(out var res))
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
            logger.LogInformation("Current '{fileCustomName}' size {currentSize} of {maxSize}",
                fileCustomName,
                GlobalFunctions.FormatSize(currentSize),
                GlobalFunctions.FormatSize(maxSize));
            return false;
        }

        logger.LogWarning("Max size {maxSize} of '{fileCustomName}' has been reached {currentSize}",
            GlobalFunctions.FormatSize(maxSize),
            fileCustomName,
            GlobalFunctions.FormatSize(currentSize));

        return true;
    }

    protected virtual async Task<IBrowser> GetBrowser(CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            logger.LogInformation("Detected Windows");
            _browserFetcher.Browser = SupportedBrowser.Chrome;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.LogInformation("Detected Linux");
            _browserFetcher.Browser = SupportedBrowser.Chromium;
        }

        logger.LogInformation("Detected installed browsers: {join}",
            string.Join(", ", _browserFetcher.GetInstalledBrowsers().Select(f => f.GetExecutablePath())));

        //UninstallBrowsers();

        if (!_browserFetcher.GetInstalledBrowsers().Any())
        {
            logger.LogInformation("Download Browser");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                await _browserFetcher.DownloadAsync(BrowserTag.Stable);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                await _browserFetcher.DownloadAsync(BrowserTag.Latest);

            logger.LogInformation("Detected installed browsers: {join}",
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

    protected virtual async Task<Result<File?>> ParseAndSaveFile(
        KeyValuePair<string, IElement> link,
        Bucket defaultBucket,
        File? lastFile,
        CancellationToken cancellationToken)
    {
        var dataNum = link.Key.Split("/").Last() == "0" ? 0 : int.Parse(link.Key.Split("/").Last());

        if ((lastFile?.CurrentPartNumber ?? -1u) >= dataNum)
        {
            logger.LogInformation("Skipping {fileCustomName} {dataNum}", FileCustomName, dataNum);
            return Result.Ok(lastFile);
        }

        logger.LogInformation("Go to {link}", link);

        var browser = await GetBrowser(cancellationToken);

        var page = await GetNewPage(browser);

        await page.GoToAsync(link.Key,
            new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 30000 });

        logger.LogInformation("Waiting for page load with images");
        await page.WaitForSelectorAsync("#fotocontext", new WaitForSelectorOptions { Timeout = 30000 });

        var html = await page.GetContentAsync();

        await CloseBrowser(browser);

        var images = (await new HtmlParser().ParseDocumentAsync(html, cancellationToken))
            .QuerySelectorAll("#fotocontext > .manga-img-placeholder > img")
            .ToImmutableList();

        logger.LogInformation("Found {images} links", images.Count);

        using var memoryStream = new MemoryStream();

        using var httpClient = httpClientFactory.CreateClient("parser-http-client");

        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var img in images.OrderBy(f => int.Parse(f.GetAttribute("data-page"))))
            {
                var src = img.GetAttribute("data-original");
                if (src.IsNullOrEmptyOrWhitespace())
                    src = img.GetAttribute("src")!;

                var imageBytes = await httpClient.GetByteArrayAsync(src, cancellationToken);

                if (ImageEmptyChecker.IsEmpty(imageBytes)) continue;

                var splitImages =
                    ImageSplitter.SplitImage(StaticFunctions.ConvertByteArrayToJpeg(imageBytes));

                foreach (var el in splitImages.Index())
                {
                    var fileName =
                        $"{img.GetAttribute("data-page")}-{el.Index}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";
                    var entry = zipArchive.CreateEntry(fileName, CompressionLevel.SmallestSize);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(el.Item, cancellationToken);
                    await entryStream.FlushAsync(cancellationToken);
                    entryStream.Close();
                }

                SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            }
        }

        await memoryStream.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        logger.LogInformation("Save to db {name}",
            $"{link.Key.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip");

        var fileUploadResult = await fileUploadService.UploadFileAsync(
            lastFile?.Id,
            null,
            lastFile?.Bucket ?? defaultBucket,
            Enum.GetValues<RoleType>().ToList(),
            memoryStream,
            $"{link.Key.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip",
            FileCustomName,
            "application/zip",
            FileType.ZipWithImg,
            MyRegex().Replace(link.Value.TextContent.Trim(), "").Trim(),
            uint.Parse(dataNum.ToString())
        );

        memoryStream.Close();

        if (fileUploadResult.IsFailed)
            return Result.Fail(
                new Error(
                    $"Error downloading {link.Key.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip"));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        await BroadcastAllSubs(context, botClient,
            $"File {FileCustomName} {MyRegex().Replace(link.Value.TextContent.Trim(), "").Trim()} has been uploaded automatically.");

        var currentSize = await CurrentSize(context, FileCustomName, cancellationToken);

        if (CheckMaxSizeReached(MaxSize, currentSize, FileCustomName))
            return Result.Fail("MaxSize reached");

        GC.Collect();
        GC.WaitForPendingFinalizers();

        return Result.Ok(fileUploadResult.Value)!;
    }

    [GeneratedRegex(@"^\d+\s*-\s*")]
    private static partial Regex MyRegex();
}
