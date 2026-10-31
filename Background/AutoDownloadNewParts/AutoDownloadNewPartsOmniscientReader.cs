using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsOmniscientReader(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    IHttpClientFactory httpClientFactory,
    ILogger<AutoDownloadNewPartsOmniscientReader> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsOmniscientReader>(
        contextFactory, fileUploadService, httpClientFactory, logger, scheduledTaskHubContext,
        taskRepository, notificationRepository)
{
    protected override string FileCustomName => "Всеведущий читатель";
    protected override string Url => "https://a.zazaza.me/vseveduchii_chitatel__A5664";
}
