using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebReader.Configuration;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models.Entities;

namespace WebReader.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public class AccountProfileController(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IOptions<TelegramBotClientConfig> telegramConfig) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var userId = User.GetUserGuid();
        var subscriber = await context.SubscriberTgs
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        return View(subscriber);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Link(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var userId = User.GetUserGuid();

        // Remove existing link if any
        var existingSubscriber = await context.SubscriberTgs
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (existingSubscriber != null)
        {
            context.SubscriberTgs.Remove(existingSubscriber);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Remove expired or used links
        context.UserTelegramLinks.RemoveRange(
            context.UserTelegramLinks
                .Where(x => x.UserId == userId && (x.IsUsed || x.ExpiresAt < DateTimeOffset.UtcNow))
        );
        await context.SaveChangesAsync(cancellationToken);

        // Generate new link code
        var link = new UserTelegramLink
        {
            UserId = userId,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        context.UserTelegramLinks.Add(link);
        await context.SaveChangesAsync(cancellationToken);

        var botUsername = telegramConfig.Value.BotUsername;
        var deepLinkUrl = $"https://t.me/{botUsername}?start={link.Code}";
        ViewBag.DeepLinkUrl = deepLinkUrl;

        return View("Index", null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlink(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var userId = User.GetUserGuid();

        var subscriber = await context.SubscriberTgs
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (subscriber != null)
        {
            context.SubscriberTgs.Remove(subscriber);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Remove active link codes
        context.UserTelegramLinks.RemoveRange(
            context.UserTelegramLinks
                .Where(x => x.UserId == userId && !x.IsUsed && x.ExpiresAt > DateTimeOffset.UtcNow)
        );
        await context.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}
