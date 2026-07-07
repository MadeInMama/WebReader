using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsWorldAfterDestruction(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    MinioService minioService,
    ITelegramBotClient botClient,
    IHttpClientFactory httpClientFactory,
    ILogger<AutoDownloadNewPartsWorldAfterDestruction> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsWorldAfterDestruction>(
        contextFactory, fileUploadService, minioService, botClient, httpClientFactory, logger, scheduledTaskHubContext,
        taskRepository)
{
    protected override string FileCustomName => "Мир после падения";
    protected override string Url => "https://3.readmanga.ru/mir_posle_padeniia";
}
