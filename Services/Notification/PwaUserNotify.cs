using WebReader.Models.Entities;

namespace WebReader.Services.Notification;

public class PwaUserNotify(ILogger<PwaUserNotify> logger) : IUserNotify
{
    public async Task<bool> SendAsync(CustomUser user, Models.Entities.Notification notification,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{} skipped, still not implemented", nameof(TgUserNotify));
        return await Task.FromResult(true);
    }
}
