using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsOmniscientReader(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ITelegramBotClient botClient,
    IHttpClientFactory httpClientFactory,
    ILogger<AutoDownloadNewPartsOmniscientReader> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsOmniscientReader>(
        contextFactory, fileUploadService, botClient, httpClientFactory, logger, scheduledTaskHubContext)
{
    protected override string FileCustomName => "Всеведущий читатель";
    protected override string Url => "https://3.readmanga.ru/vseveduchii_chitatel__A5664";
}
