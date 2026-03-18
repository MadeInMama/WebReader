using System.IO.Compression;
using System.Runtime.InteropServices;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsOmniscientReader(
    ApplicationDbContext context,
    FileUploadService fileUploadService,
    ILogger<AutoDownloadNewPartsOmniscientReader> logger) : IAutoDownloadNewParts
{
    public async Task GetAndDownload(CancellationToken stoppingToken)
    {
        const string url = "https://3.readmanga.ru/vseveduchii_chitatel__A5664";
        var maxSizeSetting =
            await context.Settings.FirstOrDefaultAsync(f => f.Key == "max_files_size_limit", stoppingToken);
        ulong maxSize = 1u * 1024u * 1024u * 1024u; //GB
        if (maxSizeSetting != null)
            maxSize = ulong.Parse(maxSizeSetting.Value);

        var currentSize = await context.Files.Select(f => f.Size).AsAsyncEnumerable()
            .AggregateAsync((f1, f2) => f1 + f2, stoppingToken);
        if (maxSize < currentSize)
        {
            logger.LogWarning("Max size {maxSize} has been reached {currentSize}", maxSize, currentSize);
            return;
        }

        var bf = new BrowserFetcher();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            logger.LogInformation("Detected Windows");
            bf.Browser = SupportedBrowser.Chrome;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.LogInformation("Detected Linux");
            bf.Browser = SupportedBrowser.Chromium;
        }

        var installed = bf.GetInstalledBrowsers().ToList();

        logger.LogInformation("Detected installed browsers: {join}",
            string.Join(", ", installed.Select(f => f.GetExecutablePath())));

        foreach (var el in installed) bf.CustomUninstall(el.Browser, el.Platform, el.BuildId, logger);

        logger.LogInformation("Download Browser");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            await bf.DownloadAsync(BrowserTag.Stable);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            await bf.DownloadAsync(BrowserTag.Latest);

        installed = bf.GetInstalledBrowsers().ToList();

        logger.LogInformation("Detected installed browsers: {join}",
            string.Join(", ", installed.Select(f => f.GetExecutablePath())));

        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--lang=en-US"
            ],
            ExecutablePath =
                bf.GetExecutablePath(installed.First().BuildId)
        });

        var page = await browser.NewPageAsync();

        await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
        {
            { "Accept-Language", "ru-RU,ru;q=0.9" },
            { "Referer", "https://readmanga.ru/" }
        });

        logger.LogInformation("Go to {url}", url);
        await page.GoToAsync(url,
            new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 0 });

        logger.LogInformation("Waiting for page load");
        await page.WaitForSelectorAsync(".chapters", new WaitForSelectorOptions { Timeout = 0 });

        var html = await page.GetContentAsync();

        var links = (await new HtmlParser().ParseDocumentAsync(html, stoppingToken))
            .QuerySelectorAll(".chapters > table .item-title > a")
            .Select(a => a.GetAttribute("href"))
            .Where(h => !GlobalFunctions.IsNullOrEmptyOrWhitespace(h))
            .Cast<string>()
            .ToList();

        logger.LogInformation("Found {links} links", links.Count);

        var lastStoredFile = await context.Files.Where(f => f.CustomName == "Всеведущий читатель")
            .Include(f => f.Bucket)
            .AsAsyncEnumerable()
            .OrderBy(f => int.Parse(f.CurrentPartName!))
            .LastOrDefaultAsync(stoppingToken);

        if (lastStoredFile != null)
        {
            var lastFile = lastStoredFile;

            foreach (var link in links.OrderBy(f => int.Parse(f.Split("/").Last())))
            {
                var dataNum = link.Split("/").Last() == "0" ? 0 : int.Parse(link.Split("/").Last());

                if (int.Parse(lastFile.CurrentPartName) >= dataNum)
                {
                    logger.LogInformation("Skipping {dataNum}", dataNum);
                    continue;
                }

                logger.LogInformation("Go to {link}", link);
                await page.GoToAsync(link,
                    new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 0 });

                logger.LogInformation("Waiting for page load with images");
                await page.WaitForSelectorAsync("#fotocontext", new WaitForSelectorOptions { Timeout = 0 });

                html = await page.GetContentAsync();

                var images = (await new HtmlParser().ParseDocumentAsync(html, stoppingToken))
                    .QuerySelectorAll("#fotocontext > .manga-img-placeholder > img")
                    .ToList();

                logger.LogInformation("Found {images} links", images.Count);

                using var memoryStream = new MemoryStream();

                using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var img in images.OrderBy(f => int.Parse(f.GetAttribute("data-page"))))
                    {
                        var src = img.GetAttribute("data-original");
                        if (GlobalFunctions.IsNullOrEmptyOrWhitespace(src))
                            src = img.GetAttribute("src")!;

                        logger.LogInformation("Downloading {src}", src);

                        var imageBytes = await new HttpClient().GetByteArrayAsync(src, stoppingToken);

                        src!.TryGetImgType(out var imageType);

                        var fileName = $"{img.GetAttribute("data-page")}.{TypeHelper.ImgTypeNameDict[imageType]}";

                        var entry = zipArchive.CreateEntry(fileName, CompressionLevel.SmallestSize);
                        await using var entryStream = entry.Open();
                        await entryStream.WriteAsync(StaticFunctions.ConvertByteArrayToGif(imageBytes), stoppingToken);
                    }
                }

                await memoryStream.FlushAsync(stoppingToken);
                memoryStream.Position = 0;

                logger.LogInformation("Save to db {name}",
                    $"{link.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip");

                var fileUploadResult = await fileUploadService.UploadFileAsync(
                    lastFile.Id,
                    null,
                    lastFile.Bucket!,
                    Enum.GetValues<RoleType>().ToList(),
                    memoryStream,
                    $"{link.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip",
                    "Всеведущий читатель",
                    "application/zip",
                    FileType.ZipWithImg,
                    dataNum.ToString()
                );

                if (!fileUploadResult.isSuccessfull)
                {
                    logger.LogError("Error downloading {name}",
                        $"{link.Replace("/", "_").Replace(":", "").Replace("https", "").Replace("http", "")}.zip");
                    goto finish;
                }

                lastFile = fileUploadResult.currentFile;

                currentSize = await context.Files.Select(f => f.Size).AsAsyncEnumerable()
                    .AggregateAsync((f1, f2) => f1 + f2, stoppingToken);
                if (maxSize < currentSize)
                {
                    logger.LogWarning("Max size {maxSize} has been reached {currentSize}", maxSize, currentSize);
                    goto finish;
                }
            }
        }

        finish:
        await browser.CloseAsync();

        foreach (var el in installed) bf.CustomUninstall(el.Browser, el.Platform, el.BuildId, logger);
    }
}
