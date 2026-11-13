using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using WebReader.Services.Http;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsSoloLevelingRagnarok(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ILogger<AutoDownloadNewPartsSoloLevelingRagnarok> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository,
    ReadMangaClubClient readMangaClubClient)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsSoloLevelingRagnarok>(
        contextFactory, fileUploadService, logger, scheduledTaskHubContext,
        taskRepository, notificationRepository, readMangaClubClient)
{
    protected override string FileCustomName => "Поднятие уровня в одиночку: Рагнарёк";
    protected override string Url => "https://1.readmanga.club/podniatie_urovnia_v_odinochku__ragnarek";
}
