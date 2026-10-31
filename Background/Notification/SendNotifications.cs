using System.Linq.Expressions;
using FluentResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services.Notification;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.Notification;

public class SendNotifications(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IEnumerable<IUserNotify> notifiers,
    ScheduledTaskRepository taskRepository,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ILogger<SendNotifications> logger)
    : AbstractBackgroundTasked<SendNotifications>(taskRepository, scheduledTaskHubContext, logger)
{
    private const int UsersBatchSize = 1000;
    private const int NotificationsSaveChangesBatch = 100;

    private static Expression<Func<Models.Entities.Notification, bool>> NotificationsPredicate => f =>
        !f.IsSent && (f.User == null || f.User.IsActive);

    public override async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        var totalSentCount = 0;
        var totalUpdatedInDb = 0;

        await using var notificationContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        var totalNotificationsCount = await notificationContext.Notifications
            .AsNoTracking()
            .CountAsync(NotificationsPredicate, cancellationToken);

        var currentIndex = 0;

        await foreach (var notification in notificationContext.Notifications
                           .Where(NotificationsPredicate)
                           .Include(f => f.User)
                           .Include(f => f.User!.SubscriberTg)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            var isSuccessful = true;

            if (notification.IsPersonal() && notification.User != null)
            {
                foreach (var notifier in notifiers)
                    if (!await notifier.SendAsync(notification.User, notification, cancellationToken))
                        isSuccessful = false;
            }
            else
            {
                var page = 0;
                while (true)
                {
                    await using var userContext = await contextFactory.CreateDbContextAsync(cancellationToken);

                    var usersBatch = await userContext.Users
                        .Where(f => f.IsActive)
                        .Include(f => f.SubscriberTg)
                        .AsNoTracking()
                        .OrderBy(f => f.Id)
                        .Skip(page * UsersBatchSize)
                        .Take(UsersBatchSize)
                        .ToListAsync(cancellationToken);

                    if (usersBatch.Count == 0)
                        break;

                    foreach (var user in usersBatch)
                    foreach (var notifier in notifiers)
                        if (!await notifier.SendAsync(user, notification, cancellationToken))
                            isSuccessful = false;

                    page++;
                }
            }

            if (isSuccessful)
            {
                notification.SetSent();
                totalSentCount++;
            }

            currentIndex++;

            if (currentIndex % NotificationsSaveChangesBatch == 0)
            {
                totalUpdatedInDb += await notificationContext.SaveChangesAsync(cancellationToken);
                notificationContext.ChangeTracker.Clear();
            }

            await UpdateProgress(task.Id, TaskStatus.InProgress, new decimal(currentIndex) / totalNotificationsCount,
                null, cancellationToken);
        }

        if (notificationContext.ChangeTracker.HasChanges())
            totalUpdatedInDb += await notificationContext.SaveChangesAsync(cancellationToken);

        return Result.Ok($"Sent count: {totalSentCount} | Updated in DB: {totalUpdatedInDb}");
    }
}
