using System.Text.Json;
using Microsoft.Extensions.Options;
using WebPush;
using WebReader.Configuration;
using WebReader.Helpers;
using WebReader.Models.Entities;

namespace WebReader.Services.Notification;

public class PwaUserNotify(
    IOptions<VapidKeysConfig> vapidKeysConfig,
    LinkGenerationService linkGenerationService,
    ILogger<PwaUserNotify> logger) : AbstractUserNotify(linkGenerationService)
{
    private readonly WebPushClient _webPushClient = new();

    public override async Task<bool> SendAsync(CustomUser user, Models.Entities.Notification notification,
        CancellationToken cancellationToken)
    {
        if (!user.SubscriberPwas.Any())
        {
            logger.LogInformation("{} skipped due to user pwa not linked", nameof(PwaUserNotify));
            return await Task.FromResult(true);
        }

        try
        {
            var vapidDetails = new VapidDetails(
                vapidKeysConfig.Value.Subject,
                vapidKeysConfig.Value.PublicKey,
                vapidKeysConfig.Value.PrivateKey);

            var payload = JsonSerializer.Serialize(new
            {
                Title = "WebReader",
                Body = notification.Message.RootElement.GetProperty(StaticJsonNames.NotificationBody).GetString() ??
                       string.Empty,
                Url = GetUrl(notification, false)
            });

            foreach (var subscriberPwa in user.SubscriberPwas)
            {
                var pushSubscription = new PushSubscription(
                    subscriberPwa.Endpoint,
                    subscriberPwa.Keys["p256dh"],
                    subscriberPwa.Keys["auth"]
                );
                try
                {
                    await _webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails,
                        cancellationToken);
                }
                catch (WebPushException e)
                {
                    logger.LogError(e, "{} not sent due to WebPush error, status={status}", nameof(PwaUserNotify),
                        e.StatusCode);
                    // Если статус 410 (Gone) или 404 (Not Found), подписка больше не активна (пользователь удалил PWA)
                    //if (e.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound) TODO: remove unavailable sub
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "{} not sent due to error", nameof(PwaUserNotify));
            return await Task.FromResult(false);
        }

        return await Task.FromResult(true);
    }
}
