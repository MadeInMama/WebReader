using Telegram.Bot;
using WebReader.Helpers;
using WebReader.Models.Entities;

namespace WebReader.Services.Notification;

public class TgUserNotify(ITelegramBotClient botClient, ILogger<TgUserNotify> logger) : IUserNotify
{
    public async Task<bool> SendAsync(CustomUser user, Models.Entities.Notification notification,
        CancellationToken cancellationToken)
    {
        if (user.SubscriberTg == null)
        {
            logger.LogInformation("{} skipped due to user tg not linked", nameof(TgUserNotify));
            return await Task.FromResult(false);
        }

        try
        {
            await botClient.SendMessage(user.SubscriberTg!.ChatId,
                notification.Message.RootElement.GetProperty(StaticJsonNames.NotificationBody).GetString() ??
                string.Empty,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "{} not sent due to error", nameof(TgUserNotify));
            return await Task.FromResult(false);
        }

        return await Task.FromResult(true);
    }
}
