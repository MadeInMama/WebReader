using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsSoloLeveling(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    IHttpClientFactory httpClientFactory,
    ILogger<AutoDownloadNewPartsSoloLeveling> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsSoloLeveling>(
        contextFactory, fileUploadService, httpClientFactory, logger, scheduledTaskHubContext,
        taskRepository, notificationRepository)
{
    protected override string FileCustomName => "Поднятие уровня в одиночку";
    protected override string Url => "https://1.readmanga.club/podniatie_urovnia_v_odinochku__A5ea4";
}
