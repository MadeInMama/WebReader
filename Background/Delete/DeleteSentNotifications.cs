using FluentResults;
using WebReader.Helpers;
using WebReader.Models.Entities;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.Delete;

public class DeleteSentNotifications(
    NotificationRepository notificationRepository,
    ILogger<DeleteSentNotifications> logger)
    : IBackgroundTasked
{
    public async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        if (!task.Settings.RootElement
                .GetProperty(StaticJsonNames.OlderThenInHours)
                .TryGetUInt16(out var days))
            return Result.Fail($"Can't get '{nameof(StaticJsonNames.OlderThenInHours)}' param.");

        var result = await notificationRepository.DeleteAllSentAndOlderThenAsync(
            DateTimeOffset.UtcNow.AddHours(-days), cancellationToken);

        logger.LogTrace($"{nameof(DeleteOldCompletedTasks)}: Deleted rows count: {{}}", result);

        return Result.Ok($"Deleted count: {result}");
    }

    public Task UpdateProgress(Guid taskId, TaskStatus status, decimal? progress, string? result,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Skipped");
        return Task.CompletedTask;
    }
}
