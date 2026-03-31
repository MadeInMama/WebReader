using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using WebReader.Data;
using WebReader.Helpers;

namespace WebReader.Background.AutoDownloadNewParts;

public abstract class AbstractAutoDownloadNewParts<T>(ILogger<T> logger) : IAutoDownloadNewParts
{
    private const ulong DefaultMaxSize = 1u * 1024u; //1gb
    private readonly BrowserFetcher _browserFetcher = new();
    protected readonly ILogger<T> Logger = logger;

    public virtual Task GetAndDownload(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }


    protected async Task<ulong> GetMaxSize(
        ApplicationDbContext context,
        string settingSizeName,
        CancellationToken stoppingToken)
    {
        var maxSizeSetting =
            await context.Settings.FirstOrDefaultAsync(f => f.Key == settingSizeName, stoppingToken);

        if (maxSizeSetting == null) return DefaultMaxSize;

        return (ulong.TryParse(maxSizeSetting.Value, out var maxSize) ? maxSize : DefaultMaxSize) * 1024u * 1024u;
    }

    protected async Task<ulong> CurrentSize(ApplicationDbContext context, string fileCustomName,
        CancellationToken stoppingToken)
    {
        return await context.Files.Where(f => f.CustomName == fileCustomName).Select(f => f.Size)
            .AsAsyncEnumerable()
            .AggregateAsync(0ul, (currentSum, nullableValue) => currentSum + (nullableValue ?? 0ul), stoppingToken);
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

    protected async Task<IBrowser> GetBrowser()
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

        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
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
}
