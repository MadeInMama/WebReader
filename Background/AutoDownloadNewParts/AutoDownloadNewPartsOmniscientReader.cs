using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using WebReader.Services.Http;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsOmniscientReader(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    ILogger<AutoDownloadNewPartsOmniscientReader> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository,
    ReadMangaClubClient readMangaClubClient)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsOmniscientReader>(
        contextFactory, fileUploadService, logger, scheduledTaskHubContext,
        taskRepository, notificationRepository, readMangaClubClient)
{
    protected override string FileCustomName => "Всеведущий читатель";
    protected override string Url => "https://a.zazaza.me/vseveduchii_chitatel__A5664";
}
