using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using WebReader.Services.Http;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsSoloLevelingHunterOrigin(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ILogger<AutoDownloadNewPartsSoloLevelingHunterOrigin> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository,
    ReadMangaClubClient readMangaClubClient)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsSoloLevelingHunterOrigin>(
        contextFactory, fileUploadService, logger, scheduledTaskHubContext,
        taskRepository, notificationRepository, readMangaClubClient)
{
    protected override string FileCustomName => "Поднятие уровня в одиночку: Истоки Охотника";
    protected override string Url => "https://1.readmanga.club/podniatie_urovnia_v_odinochku__istoki_ohotnika";
}
