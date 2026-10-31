using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using WebReader.Configuration;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;

namespace WebReader.Controllers;

[ApiController]
[Route("api/tgwh")]
public class TelegramWebhookController(
    SubscriberTgRepository subscriberTgRepository,
    UserTgLinkRepository userTgLinkRepository,
    ITelegramBotClient botClient,
    ILogger<TelegramWebhookController> logger,
    IOptions<TelegramBotClientConfig> telegramBotClientConfig,
    IHubContext<UserTgLinkHub> userTgLinkHubContext)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> HandleUpdate([FromBody] Update update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var secretToken = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
            var expectedToken = telegramBotClientConfig.Value.SecretToken!;

            if (string.IsNullOrEmpty(secretToken) || secretToken != expectedToken)
            {
                logger.LogWarning("Invalid Telegram secret token received");
                return Unauthorized();
            }

            if (update.Message is { Text: { } text } message)
            {
                if (text.StartsWith("/start"))
                    await HandleStartCommand(message.Chat.Id, message.From, text[6..].Trim(), cancellationToken);
                else if (text.StartsWith("/unsubscribe"))
                    await HandleUnsubscribeCommand(message.Chat.Id, cancellationToken);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Telegram update");
            return StatusCode(500);
        }
    }

    private async Task HandleStartCommand(long chatId, User? user, string startParameter,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(startParameter, out var linkCode))
        {
            //TODO: maybe AllAsync with take 1 skip 0 orderBy ExpiresAt OR compare with all link for user
            var userTelegramLink = await userTgLinkRepository.FirstOrDefaultAsync(
                f => f.Code == linkCode && !f.IsUsed && f.ExpiresAt > DateTimeOffset.UtcNow, null, cancellationToken);

            if (userTelegramLink != null)
            {
                userTelegramLink.IsUsed = true;
                userTelegramLink.UsedAt = DateTimeOffset.UtcNow;

                var expectedSubscriber =
                    await subscriberTgRepository.FirstOrDefaultAsync(f => f.ChatId == chatId, null, cancellationToken);

                if (expectedSubscriber != null)
                    expectedSubscriber.UserId = userTelegramLink.UserId;
                else
                    await subscriberTgRepository.AddAsync(new SubscriberTg
                    {
                        ChatId = chatId,
                        UserId = userTelegramLink.UserId
                    }, cancellationToken);

                await subscriberTgRepository.SaveChangesAsync(cancellationToken);
                await userTgLinkRepository.SaveChangesAsync(cancellationToken);

                await botClient.SendMessage(chatId, "Telegram account linked successfully!",
                    cancellationToken: cancellationToken);

                await userTgLinkHubContext.Clients.User(userTelegramLink.UserId.ToString())
                    .SendAsync("UserTgLinkHub", cancellationToken);

                return;
            }

            await botClient.SendMessage(chatId,
                "Link code expired or invalid. Go to the website and generate a new one.",
                cancellationToken: cancellationToken);

            return;
        }

        var existingSubscriber =
            await subscriberTgRepository.FirstOrDefaultAsync(f => f.ChatId == chatId, null, cancellationToken);

        if (existingSubscriber != null)
        {
            await botClient.SendMessage(chatId, "You already subscribed.", cancellationToken: cancellationToken);
            return;
        }

        await subscriberTgRepository.AddAsync(new SubscriberTg
        {
            ChatId = chatId
        }, cancellationToken);

        await subscriberTgRepository.SaveChangesAsync(cancellationToken);

        await botClient.SendMessage(chatId, $"Welcome, {user?.FirstName}! You're now subscribed to updates.",
            cancellationToken: cancellationToken);
    }

    private async Task HandleUnsubscribeCommand(long chatId, CancellationToken cancellationToken)
    {
        var deletedCount =
            await subscriberTgRepository.DeleteByChatIdAsync(chatId, cancellationToken);

        if (deletedCount == 0)
            await botClient.SendMessage(chatId, "You already unsubscribed.", cancellationToken: cancellationToken);
        else
            await botClient.SendMessage(chatId, "You've been unsubscribed. Type /start to subscribe again.",
                cancellationToken: cancellationToken);
    }
}
