using FluentResults;
using WebReader.Models.Entities;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.Delete;

public class DeleteOldCompletedTasks(IServiceProvider services, ILogger<DeleteOldCompletedTasks> logger)
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
            DateTimeOffset.UtcNow.AddHours(-days), [TaskStatus.Completed, TaskStatus.Canceled], cancellationToken);

        logger.LogTrace($"{nameof(DeleteOldCompletedTasks)}: Deleted rows count: {{}}", result);

        return Result.Ok($"Deleted count: {result}");
    }
}
