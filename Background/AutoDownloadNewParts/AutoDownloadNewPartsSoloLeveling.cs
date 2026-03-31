using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using Telegram.Bot;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsSoloLeveling(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ILogger<AutoDownloadNewPartsSoloLeveling> logger,
    ITelegramBotClient botClient)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsSoloLeveling>(logger)
{
    private const string SettingSizeName = "max_files_size_limit_solo_leveling";
    private const string FileCustomName = "Поднятие уровня в одиночку";
    private const string Url = "https://3.readmanga.ru/podniatie_urovnia_v_odinochku__A5ea4";

    public override async Task GetAndDownload(CancellationToken stoppingToken)
    {
        try
        {
            var context = await contextFactory.CreateDbContextAsync(stoppingToken);

            var maxSize = await GetMaxSize(context, SettingSizeName, stoppingToken);

            var currentSize = await CurrentSize(context, FileCustomName, stoppingToken);

            if (CheckMaxSizeReached(maxSize, currentSize, FileCustomName)) return;

            var browser = await GetBrowser();

            var page = await GetNewPage(browser);

            Logger.LogInformation("Go to {url}", Url);
            await page.GoToAsync(Url,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 30000 });

            Logger.LogInformation("Waiting for page load");
            await page.WaitForSelectorAsync(".chapters", new WaitForSelectorOptions { Timeout = 30000 });

            var html = await page.GetContentAsync();

            await browser.CloseAsync();
            await browser.DisposeAsync();

            var links = (await new HtmlParser().ParseDocumentAsync(html, stoppingToken))
                .QuerySelectorAll(".chapters > table .item-title > a")
                .Where(f => f.GetAttribute("href") != null)
                .ToDictionary(f => f.GetAttribute("href")!, f => f)
                .Where(f => !GlobalFunctions.IsNullOrEmptyOrWhitespace(f.Key))
                .Where(f => int.TryParse(f.Key.Split("/").Last(), out _))
                .OrderBy(f => int.Parse(f.Key.Split("/").Last()))
                .ToImmutableList();

            Logger.LogInformation("Found {links} links", links.Count);

            var defaultBucket = await context.Buckets.FirstAsync(b => b.Name == "mybucket", stoppingToken);

            var lastStoredFile = await context.Files.Where(f => f.CustomName == FileCustomName)
                .Include(f => f.Bucket)
                .AsAsyncEnumerable()
                .OrderBy(f => f.CurrentPartNumber!)
                .LastOrDefaultAsync(stoppingToken);

            var lastFile = lastStoredFile;

            foreach (var link in links)
            {
                var dataNum = link.Key.Split("/").Last() == "0" ? 0 : int.Parse(link.Key.Split("/").Last());

                if ((lastFile?.CurrentPartNumber ?? -1u) >= dataNum)
                {
                    logger.LogInformation("Skipping {dataNum}", dataNum);
                    continue;
                }

                Logger.LogInformation("Go to {link}", link);

                browser = await GetBrowser();

                page = await GetNewPage(browser);

                await page.GoToAsync(link.Key,
                    new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 30000 });

                Logger.LogInformation("Waiting for page load with images");
                await page.WaitForSelectorAsync("#fotocontext", new WaitForSelectorOptions { Timeout = 30000 });

                html = await page.GetContentAsync();

                await browser.CloseAsync();
                await browser.DisposeAsync();

                var images = (await new HtmlParser().ParseDocumentAsync(html, stoppingToken))
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

                        Logger.LogInformation("Downloading {src}", src);

                        var imageBytes = await new HttpClient().GetByteArrayAsync(src, stoppingToken);

                        if (ImageEmptyChecker.IsEmpty(imageBytes)) continue;

                        var splitImages =
                            ImageSplitter.SplitImage(StaticFunctions.ConvertByteArrayToJpeg(imageBytes));

                        foreach (var el in splitImages.Index())
                        {
                            var fileName =
                                $"{img.GetAttribute("data-page")}-{el.Index}.{TypeHelper.ImgTypeNameDict[ImageType.Jpeg]}";
                            var entry = zipArchive.CreateEntry(fileName, CompressionLevel.SmallestSize);
                            await using var entryStream = entry.Open();
                            await entryStream.WriteAsync(el.Item, stoppingToken);
                        }
                    }
                }

                await memoryStream.FlushAsync(stoppingToken);
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
                    FileCustomName,
                    "application/zip",
                    FileType.ZipWithImg,
                    Regex.Replace(link.Value.TextContent.Trim(), @"^\d+\s*-\s*", "").Trim(),
                    uint.Parse(dataNum.ToString())
                );

                if (!fileUploadResult.isSuccessfull)
                {
                    Logger.LogError("Error downloading {name}",
                        $"{link.Key.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip");
                    break;
                }

                await BroadcastAllSubs(context, botClient,
                    $"File {FileCustomName} {Regex.Replace(link.Value.TextContent.Trim(), @"^\d+\s*-\s*", "").Trim()} has been uploaded automatically.");

                lastFile = fileUploadResult.currentFile;

                context = await contextFactory.CreateDbContextAsync(stoppingToken);

                currentSize = await CurrentSize(context, FileCustomName, stoppingToken);

                if (CheckMaxSizeReached(maxSize, currentSize, FileCustomName)) break;

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // UninstallBrowsers();
        }
        catch (TimeoutException e)
        {
            Logger.LogError("Timeout has been reached: {}", e.Message);
            // UninstallBrowsers();
        }
        catch (NavigationException e)
        {
            Logger.LogError("Navigation error has been reached: {}", e.Message);
            // UninstallBrowsers();
        }
        catch (HttpRequestException e)
        {
            Logger.LogError("HttpRequest error has been reached: {}", e.Message);
            // UninstallBrowsers();
        }
    }
}
