using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Controllers;

[ApiController]
[Route("api/tgwh")]
public class TelegramWebhookController(
    ITelegramBotClient botClient,
    ILogger<TelegramWebhookController> logger,
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> HandleUpdate([FromBody] Update update)
    {
        try
        {
            if (update.Message is { } message)
                if (message.Text is { } text)
                {
                    if (text.StartsWith("/start"))
                        await HandleStartCommand(message.Chat.Id, message.From);
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

    private async Task HandleStartCommand(long chatId, User? user)
    {
        var context = await contextFactory.CreateDbContextAsync();

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
        var context = await contextFactory.CreateDbContextAsync();

        var subscriberTg = await context.SubscriberTgs.FirstOrDefaultAsync(x => x.ChatId == chatId);

        if (subscriberTg != null)
        {
            context.SubscriberTgs.Remove(subscriberTg);
            await context.SaveChangesAsync();
        }

        await botClient.SendMessage(
            chatId,
            "You've been unsubscribed. Type /start to subscribe again."
        );
    }
}
