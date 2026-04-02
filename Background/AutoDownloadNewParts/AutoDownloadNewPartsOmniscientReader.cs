using System.Collections.Immutable;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using Telegram.Bot;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsOmniscientReader(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ILogger<AutoDownloadNewPartsOmniscientReader> logger,
    ITelegramBotClient botClient,
    IHttpClientFactory httpClientFactory)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsOmniscientReader>(logger, httpClientFactory)
{
    private const string SettingSizeName = "max_files_size_limit_vseveduschiy_chitatel";
    private const string FileCustomName = "Всеведущий читатель";
    private const string Url = "https://3.readmanga.ru/vseveduchii_chitatel__A5664";

    public override async Task GetAndDownload(CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var maxSize = await GetMaxSize(context, SettingSizeName, cancellationToken);

            var currentSize = await CurrentSize(context, FileCustomName, cancellationToken);

            if (CheckMaxSizeReached(maxSize, currentSize, FileCustomName)) return;

            var browser = await GetBrowser(cancellationToken);

            var page = await GetNewPage(browser);

            Logger.LogInformation("Go to {url}", Url);
            await page.GoToAsync(Url,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 30000 });

            Logger.LogInformation("Waiting for page load");
            await page.WaitForSelectorAsync(".chapters", new WaitForSelectorOptions { Timeout = 30000 });

            var html = await page.GetContentAsync();

            await CloseBrowser(browser);

            var links = (await new HtmlParser().ParseDocumentAsync(html, cancellationToken))
                .QuerySelectorAll(".chapters > table .item-title > a")
                .Where(f => f.GetAttribute("href") != null)
                .ToDictionary(f => f.GetAttribute("href")!, f => f)
                .Where(f => !GlobalFunctions.IsNullOrEmptyOrWhitespace(f.Key))
                .Where(f => int.TryParse(f.Key.Split("/").Last(), out _))
                .OrderBy(f => int.Parse(f.Key.Split("/").Last()))
                .ToImmutableList();

            Logger.LogInformation("Found {links} links", links.Count);

            var defaultBucket = await context.Buckets.FirstAsync(b => b.Name == "mybucket", cancellationToken);

            var lastStoredFile = await context.Files.Where(f => f.CustomName == FileCustomName)
                .Include(f => f.Bucket)
                .AsAsyncEnumerable()
                .OrderBy(f => f.CurrentPartNumber!)
                .LastOrDefaultAsync(cancellationToken);

            var lastFile = lastStoredFile;

            foreach (var link in links)
            {
                var res = await ParseAndSaveFile(contextFactory, fileUploadService, botClient, link,
                    defaultBucket, lastFile, FileCustomName, SettingSizeName, cancellationToken);

                if (res.IsFailed)
                {
                    Logger.LogError("{msg}", res.ToString());

                    break;
                }

                lastFile = res.ValueOrDefault;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            //UninstallBrowsers();
        }
        catch (TimeoutException e)
        {
            Logger.LogError("Timeout has been reached: {}", e.Message);
            //UninstallBrowsers();
        }
        catch (NavigationException e)
        {
            Logger.LogError("Navigation error has been reached: {}", e.Message);
            //UninstallBrowsers();
        }
        catch (HttpRequestException e)
        {
            Logger.LogError("HttpRequest error has been reached: {}", e.Message);
            //UninstallBrowsers();
        }
        finally
        {
            BrowserProcessKiller.PrepareCleanBrowserEnvironment(logger);
            SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
