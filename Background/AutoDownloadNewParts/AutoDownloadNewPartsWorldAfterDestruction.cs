using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsWorldAfterDestruction(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    FileUploadService fileUploadService,
    IHttpClientFactory httpClientFactory,
    ILogger<AutoDownloadNewPartsWorldAfterDestruction> logger,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ScheduledTaskRepository taskRepository,
    NotificationRepository notificationRepository)
    : AbstractAutoDownloadNewParts<AutoDownloadNewPartsWorldAfterDestruction>(
        contextFactory, fileUploadService, httpClientFactory, logger, scheduledTaskHubContext,
        taskRepository, notificationRepository)
{
    protected override string FileCustomName => "Мир после падения";
    protected override string Url => "https://a.zazaza.me/mir_posle_padeniia";
}
