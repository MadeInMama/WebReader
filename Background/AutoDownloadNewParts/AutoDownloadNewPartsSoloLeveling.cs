using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsSoloLeveling(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ITelegramBotClient botClient,
    IHttpClientFactory httpClientFactory,
    ILogger<AutoDownloadNewPartsSoloLeveling> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsSoloLeveling>(
        contextFactory, fileUploadService, botClient, httpClientFactory, logger, scheduledTaskHubContext,
        taskRepository)
{
    protected override string FileCustomName => "Поднятие уровня в одиночку";
    protected override string Url => "https://3.readmanga.ru/podniatie_urovnia_v_odinochku__A5ea4";
}
