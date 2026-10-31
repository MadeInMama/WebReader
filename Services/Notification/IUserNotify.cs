using WebReader.Models.Entities;

namespace WebReader.Services.Notification;

public interface IUserNotify
{
    Task<bool> SendAsync(CustomUser user, Models.Entities.Notification notification,
        CancellationToken cancellationToken);
}
