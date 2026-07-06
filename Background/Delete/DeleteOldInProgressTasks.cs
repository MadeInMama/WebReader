using FluentResults;
using WebReader.Models.Entities;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.Delete;

public class DeleteOldInProgressTasks(IServiceProvider services, ILogger<DeleteOldInProgressTasks> logger)
    : IBackgroundTasked
{
    private const string SettingOlderThenInHoursToDelete = "older_then_in_hours";

    public async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        if (!task.Settings.RootElement
                .GetProperty(SettingOlderThenInHoursToDelete)
                .TryGetUInt16(out var days))
            return Result.Fail($"Can't get '{nameof(SettingOlderThenInHoursToDelete)}' param.");

        using var scope = services.CreateScope();
        var scheduledTaskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();

        var result = await scheduledTaskRepository.DeleteAllOlderThenAsync(
            DateTimeOffset.UtcNow.AddHours(-days), [TaskStatus.InProgress], cancellationToken);

        logger.LogTrace($"{nameof(DeleteOldErroredTasks)}: Deleted rows count: {{}}", result);

        return Result.Ok($"Deleted count: {result}");
    }

    public Task UpdateProgress(Guid taskId, TaskStatus status, decimal? progress, string? result,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Skipped");
        return Task.CompletedTask;
    }
}
