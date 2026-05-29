using FluentResults;
using WebReader.Models.Entities;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.Delete;

public class DeleteOldErroredTasks(IServiceProvider services, ILogger<DeleteOldErroredTasks> logger)
    : IBackgroundTasked
{
    private const string SettingOlderThenInDaysToDelete = "older_then_in_hours";

    public async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        if (task.ScheduledTaskConfig == null || !task.ScheduledTaskConfig.DefaultSettings.RootElement
                .GetProperty(SettingOlderThenInDaysToDelete).TryGetUInt16(out var days))
            return Result.Fail($"Can't get '{nameof(SettingOlderThenInDaysToDelete)}' param.");

        using var scope = services.CreateScope();
        var scheduledTaskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();

        var result = await scheduledTaskRepository.DeleteAllOlderThenAsync(
            DateTimeOffset.UtcNow.AddHours(-days), [TaskStatus.Error], cancellationToken);

        logger.LogTrace($"{nameof(DeleteOldErroredTasks)}: Deleted rows count: {{}}", result);

        return Result.Ok($"Deleted count: {result}");
    }
}