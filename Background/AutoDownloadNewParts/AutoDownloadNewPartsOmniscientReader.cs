using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using WebReader.Data;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsOmniscientReader(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ITelegramBotClient botClient,
    IHttpClientFactory httpClientFactory,
    ILogger<AutoDownloadNewPartsOmniscientReader> logger)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsOmniscientReader>(
        contextFactory, fileUploadService, botClient, httpClientFactory, logger)
{
    protected override string FileCustomName => "Всеведущий читатель";
    protected override string Url => "https://3.readmanga.ru/vseveduchii_chitatel__A5664";
}
