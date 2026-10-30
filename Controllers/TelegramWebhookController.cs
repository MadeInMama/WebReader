using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using WebReader.Configuration;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Controllers;

[ApiController]
[Route("api/tgwh")]
public class TelegramWebhookController(
    ITelegramBotClient botClient,
    ILogger<TelegramWebhookController> logger,
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IOptions<TelegramBotClientConfig> telegramBotClientConfig)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> HandleUpdate([FromBody] Update update)
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
                    await HandleStartCommand(message.Chat.Id, message.From, text[6..].Trim());
                else if (text.StartsWith("/unsubscribe"))
                    await HandleUnsubscribeCommand(message.Chat.Id);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Telegram update");
            return StatusCode(500);
        }
    }

    private async Task HandleStartCommand(long chatId, User? user, string startParameter)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        // Try to parse as deep link code (GUID)
        if (Guid.TryParse(startParameter, out var linkCode))
        {
            var link = await context.UserTelegramLinks
                .FirstOrDefaultAsync(x => x.Code == linkCode && !x.IsUsed && x.ExpiresAt > DateTimeOffset.UtcNow);

            if (link != null)
            {
                // Mark link as used
                link.IsUsed = true;
                link.UsedAt = DateTimeOffset.UtcNow;

                // Find or create subscriber and link to user
                var subscriber = await context.SubscriberTgs
                    .FirstOrDefaultAsync(x => x.ChatId == chatId);

                if (subscriber != null)
                    subscriber.UserId = link.UserId;
                else
                    context.SubscriberTgs.Add(new SubscriberTg
                    {
                        ChatId = chatId,
                        UserId = link.UserId
                    });

                await botClient.SendMessage(chatId, "Telegram account linked successfully! ✅");

                await context.SaveChangesAsync();
                return;
            }

            await botClient.SendMessage(chatId,
                "Link code expired or invalid. Go to the website and generate a new one.");
            return;
        }

        // Regular /start logic
        if (await context.SubscriberTgs.AnyAsync(x => x.ChatId == chatId))
        {
            await botClient.SendMessage(chatId, "You already subscribed.");
            return;
        }

        context.SubscriberTgs.Add(new SubscriberTg
        {
            ChatId = chatId
        });

        await context.SaveChangesAsync();

        await botClient.SendMessage(
            chatId,
            $"Welcome, {user?.FirstName}! You're now subscribed to updates. 🎉"
        );
    }

    private async Task HandleUnsubscribeCommand(long chatId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var subscriberTg = await context.SubscriberTgs.FirstOrDefaultAsync(x => x.ChatId == chatId);

        if (subscriberTg != null)
        {
            context.SubscriberTgs.Remove(subscriberTg);
            await context.SaveChangesAsync();
            await botClient.SendMessage(
                chatId,
                "You've been unsubscribed. Type /start to subscribe again."
            );
        }
        else
        {
            await botClient.SendMessage(chatId, "You already unsubscribed.");
        }
    }
}
