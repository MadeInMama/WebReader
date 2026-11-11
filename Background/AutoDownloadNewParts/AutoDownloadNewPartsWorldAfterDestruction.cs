using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using WebReader.Services.Http;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsWorldAfterDestruction(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ILogger<AutoDownloadNewPartsWorldAfterDestruction> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository,
    ReadMangaClubClient readMangaClubClient)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsWorldAfterDestruction>(
        contextFactory, fileUploadService, logger, scheduledTaskHubContext,
        taskRepository, notificationRepository, readMangaClubClient)
{
    protected override string FileCustomName => "Мир после падения";
    protected override string Url => "https://a.zazaza.me/mir_posle_padeniia";
}
