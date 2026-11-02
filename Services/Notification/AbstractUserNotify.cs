using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Entities;

namespace WebReader.Services.Notification;

public abstract class AbstractUserNotify(LinkGenerationService linkGenerationService) : IUserNotify
{
    public abstract Task<bool> SendAsync(CustomUser user, Models.Entities.Notification notification,
        CancellationToken cancellationToken);

    protected string? GetUrl(Models.Entities.Notification notification)
    {
        switch (notification.Type)
        {
            case NotificationType.System:
                return null;
            case NotificationType.NewFile:
                var bucketId = notification.Message.RootElement.GetProperty(StaticJsonNames.NotificationBucketId)
                    .GetString();
                var fileId = notification.Message.RootElement.GetProperty(StaticJsonNames.NotificationFileId)
                    .GetString();
                if (bucketId == null || fileId == null) return null;
                return linkGenerationService.GenerateGetFileUrl(Guid.Parse(bucketId), Guid.Parse(fileId));
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
