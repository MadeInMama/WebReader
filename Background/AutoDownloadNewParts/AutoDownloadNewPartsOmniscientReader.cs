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
    MinioService minioService,
    ApplicationDbContext context,
    FileUploadService fileUploadService) : IAutoDownloadNewParts
{
    public async Task GetAndDownload(CancellationToken stoppingToken)
    {
        const string url = "https://3.readmanga.ru/vseveduchii_chitatel__A5664";
        var bf = new BrowserFetcher();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            bf.Browser = SupportedBrowser.Chrome;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            bf.Browser = SupportedBrowser.Chromium;

        var installed = bf.GetInstalledBrowsers().ToList();
        foreach (var el in installed) bf.CustomUninstall(el.Browser, el.Platform, el.BuildId);

        await bf.DownloadAsync(BrowserTag.Stable);

        installed = bf.GetInstalledBrowsers().ToList();

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

        await page.GoToAsync(url,
            new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded] });

        await page.WaitForSelectorAsync(".chapters", new WaitForSelectorOptions { Timeout = 0 });

        var html = await page.GetContentAsync();

        var links = (await new HtmlParser().ParseDocumentAsync(html, stoppingToken))
            .QuerySelectorAll(".chapters > table .item-title > a")
            .Select(a => a.GetAttribute("href"))
            .Where(h => !GlobalFunctions.IsNullOrEmptyOrWhitespace(h))
            .Cast<string>()
            .ToList();

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
                    continue;

                await page.GoToAsync(link,
                    new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded] });

                await page.WaitForSelectorAsync("#fotocontext", new WaitForSelectorOptions { Timeout = 0 });

                html = await page.GetContentAsync();

                var images = (await new HtmlParser().ParseDocumentAsync(html, stoppingToken))
                    .QuerySelectorAll("#fotocontext > .manga-img-placeholder > img")
                    .ToList();

                using var memoryStream = new MemoryStream();

                using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var img in images.OrderBy(f => int.Parse(f.GetAttribute("data-page"))))
                    {
                        var src = img.GetAttribute("data-original");
                        if (GlobalFunctions.IsNullOrEmptyOrWhitespace(src))
                            src = img.GetAttribute("src")!;

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

                if (!fileUploadResult.isSuccessfull) goto finish;

                lastFile = fileUploadResult.currentFile;
            }
        }

        finish:
        await browser.CloseAsync();

        foreach (var el in installed) bf.CustomUninstall(el.Browser, el.Platform, el.BuildId);
    }
}
