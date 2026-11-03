using System.Net;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using WebReader.Helpers;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services.Notification;

public class TgUserNotify(
    ITelegramBotClient botClient,
    LinkGenerationService linkGenerationService,
    SubscriberTgRepository subscriberTgRepository,
    ILogger<TgUserNotify> logger) : AbstractUserNotify(linkGenerationService)
{
    public override async Task<bool> SendAsync(CustomUser user, Models.Entities.Notification notification,
        CancellationToken cancellationToken)
    {
        if (user.SubscriberTg == null)
        {
            logger.LogInformation("{} skipped due to user tg not linked", nameof(TgUserNotify));
            return await Task.FromResult(true);
        }

        try
        {
            var notificationBody = notification.Message.RootElement
                .GetProperty(StaticJsonNames.NotificationBody).GetString() ?? string.Empty;

            var link = GetUrl(notification, true);

            var htmlMessage = $"{notificationBody}";

            if (!string.IsNullOrWhiteSpace(link)) htmlMessage = $"<a href=\"{link}\">{notificationBody}</a>";

            await botClient.SendMessage(
                user.SubscriberTg!.ChatId,
                htmlMessage,
                ParseMode.Html,
                cancellationToken: cancellationToken
            );
        }
        catch (ApiRequestException e)
        {
            logger.LogError(e, "{} not sent due to Telegram error, status={status}", nameof(TgUserNotify),
                e.ErrorCode);

            if (e.ErrorCode == (int)HttpStatusCode.Forbidden)
                await subscriberTgRepository.DeleteAsync(user.SubscriberTg!.Id, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "{} not sent due to error", nameof(TgUserNotify));
            return await Task.FromResult(false);
        }

        return await Task.FromResult(true);
    }
}
