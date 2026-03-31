using System.Collections.Immutable;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
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

public abstract class AbstractAutoDownloadNewParts<T>(ILogger<T> logger, IHttpClientFactory httpClientFactory)
    : IAutoDownloadNewParts
{
    private const ulong DefaultMaxSize = 1u * 1024u; //1gb
    private readonly BrowserFetcher _browserFetcher = new();
    protected readonly ILogger<T> Logger = logger;

    public virtual Task GetAndDownload(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected async Task<ulong> GetMaxSize(
        ApplicationDbContext context,
        string settingSizeName,
        CancellationToken cancellationToken)
    {
        var maxSizeSetting =
            await context.Settings.FirstOrDefaultAsync(f => f.Key == settingSizeName, cancellationToken);

        if (maxSizeSetting == null) return DefaultMaxSize;

        return (ulong.TryParse(maxSizeSetting.Value, out var maxSize) ? maxSize : DefaultMaxSize) * 1024u * 1024u;
    }

    protected async Task<ulong> CurrentSize(ApplicationDbContext context, string fileCustomName,
        CancellationToken cancellationToken)
    {
        return await context.Files.Where(f => f.CustomName == fileCustomName).Select(f => f.Size)
            .AsAsyncEnumerable()
            .AggregateAsync(0ul, (currentSum, nullableValue) => currentSum + (nullableValue ?? 0ul), cancellationToken);
    }

    protected bool CheckMaxSizeReached(ulong maxSize, ulong currentSize, string fileCustomName)
    {
        if (maxSize >= currentSize)
        {
            Logger.LogInformation("Current '{fileCustomName}' size {currentSize} of {maxSize}",
                fileCustomName,
                GlobalFunctions.FormatSize(currentSize),
                GlobalFunctions.FormatSize(maxSize));
            return false;
        }

        Logger.LogWarning("Max size {maxSize} of '{fileCustomName}' has been reached {currentSize}",
            GlobalFunctions.FormatSize(maxSize),
            fileCustomName,
            GlobalFunctions.FormatSize(currentSize));

        return true;
    }

    protected async Task<IBrowser> GetBrowser(CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Logger.LogInformation("Detected Windows");
            _browserFetcher.Browser = SupportedBrowser.Chrome;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Logger.LogInformation("Detected Linux");
            _browserFetcher.Browser = SupportedBrowser.Chromium;
        }

        Logger.LogInformation("Detected installed browsers: {join}",
            string.Join(", ", _browserFetcher.GetInstalledBrowsers().Select(f => f.GetExecutablePath())));

        //UninstallBrowsers();

        if (!_browserFetcher.GetInstalledBrowsers().Any())
        {
            Logger.LogInformation("Download Browser");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                await _browserFetcher.DownloadAsync(BrowserTag.Stable);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                await _browserFetcher.DownloadAsync(BrowserTag.Latest);

            Logger.LogInformation("Detected installed browsers: {join}",
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

    protected void UninstallBrowsers()
    {
        foreach (var el in _browserFetcher.GetInstalledBrowsers())
            _browserFetcher.CustomUninstall(el.Browser, el.Platform, el.BuildId, Logger);
    }

    protected async Task<IPage> GetNewPage(IBrowser browser)
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

    protected async Task BroadcastAllSubs(ApplicationDbContext context, ITelegramBotClient botClient, string message)
    {
        var subscribers = await context.SubscriberTgs.ToListAsync();

        foreach (var el in subscribers)
            try
            {
                await botClient.SendMessage(el.ChatId, message);
                await Task.Delay(30);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 403)
            {
                var subscriberTg = await context.SubscriberTgs.FirstOrDefaultAsync(x => x.ChatId == el.ChatId);

                if (subscriberTg != null)
                {
                    context.SubscriberTgs.Remove(subscriberTg);
                    await context.SaveChangesAsync();
                }
            }
    }

    protected async Task CloseBrowser(IBrowser browser)
    {
        await browser.CloseAsync();
        await browser.DisposeAsync();
        BrowserProcessKiller.PrepareCleanBrowserEnvironment(Logger);
    }

    protected virtual async Task<(bool isSuccessful, File? lastFile)> ParseAndSaveFile(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        FileUploadService fileUploadService,
        ITelegramBotClient botClient,
        KeyValuePair<string, IElement> link,
        Bucket defaultBucket,
        File? lastFile,
        ApplicationDbContext context,
        string fileCustomName,
        string settingSizeName,
        CancellationToken cancellationToken)
    {
        var dataNum = link.Key.Split("/").Last() == "0" ? 0 : int.Parse(link.Key.Split("/").Last());

        if ((lastFile?.CurrentPartNumber ?? -1u) >= dataNum)
        {
            Logger.LogInformation("Skipping {fileCustomName} {dataNum}", fileCustomName, dataNum);
            return (true, lastFile);
        }

        Logger.LogInformation("Go to {link}", link);

        var browser = await GetBrowser(cancellationToken);

        var page = await GetNewPage(browser);

        await page.GoToAsync(link.Key,
            new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 30000 });

        Logger.LogInformation("Waiting for page load with images");
        await page.WaitForSelectorAsync("#fotocontext", new WaitForSelectorOptions { Timeout = 30000 });

        var html = await page.GetContentAsync();

        await CloseBrowser(browser);

        var images = (await new HtmlParser().ParseDocumentAsync(html, cancellationToken))
            .QuerySelectorAll("#fotocontext > .manga-img-placeholder > img")
            .ToImmutableList();

        Logger.LogInformation("Found {images} links", images.Count);

        using var memoryStream = new MemoryStream();

        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var img in images.OrderBy(f => int.Parse(f.GetAttribute("data-page"))))
            {
                var src = img.GetAttribute("data-original");
                if (GlobalFunctions.IsNullOrEmptyOrWhitespace(src))
                    src = img.GetAttribute("src")!;

                // Logger.LogInformation("Downloading {src}", src);

                var imageBytes = await httpClientFactory.CreateClient("parser-http-client")
                    .GetByteArrayAsync(src, cancellationToken);

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
                }
            }
        }

        await memoryStream.FlushAsync(cancellationToken);
        memoryStream.Position = 0;

        Logger.LogInformation("Save to db {name}",
            $"{link.Key.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip");

        var fileUploadResult = await fileUploadService.UploadFileAsync(
            lastFile?.Id,
            null,
            lastFile?.Bucket ?? defaultBucket,
            Enum.GetValues<RoleType>().ToList(),
            memoryStream,
            $"{link.Key.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip",
            fileCustomName,
            "application/zip",
            FileType.ZipWithImg,
            Regex.Replace(link.Value.TextContent.Trim(), @"^\d+\s*-\s*", "").Trim(),
            uint.Parse(dataNum.ToString())
        );

        memoryStream.Close();

        if (!fileUploadResult.isSuccessfull)
        {
            Logger.LogError("Error downloading {name}",
                $"{link.Key.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip");
            return (false, lastFile);
        }

        await BroadcastAllSubs(context, botClient,
            $"File {fileCustomName} {Regex.Replace(link.Value.TextContent.Trim(), @"^\d+\s*-\s*", "").Trim()} has been uploaded automatically.");

        context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var maxSize = await GetMaxSize(context, settingSizeName, cancellationToken);

        var currentSize = await CurrentSize(context, fileCustomName, cancellationToken);

        if (CheckMaxSizeReached(maxSize, currentSize, fileCustomName))
            return (false, lastFile);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        return (true, fileUploadResult.currentFile);
    }
}
