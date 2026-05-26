using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsWorldAfterDestruction(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ITelegramBotClient botClient,
    IHttpClientFactory httpClientFactory,
    ILogger<AutoDownloadNewPartsWorldAfterDestruction> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsWorldAfterDestruction>(
        contextFactory, fileUploadService, botClient, httpClientFactory, logger, scheduledTaskHubContext)
{
    protected override string FileCustomName => "Мир после падения";
    protected override string Url => "https://3.readmanga.ru/mir_posle_padeniia";
}
