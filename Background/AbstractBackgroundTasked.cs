using FluentResults;
using Microsoft.AspNetCore.SignalR;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background;

public abstract class AbstractBackgroundTasked<T>(
    ScheduledTaskRepository taskRepository,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ILogger<T> logger)
    : IBackgroundTasked
{
    private decimal _prevProgress;

    public abstract Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken);

    public virtual async Task UpdateProgress(Guid taskId, TaskStatus status, decimal? progress, string? result,
        CancellationToken cancellationToken)
    {
        if (_prevProgress >= progress) return;

        try
        {
            await taskRepository.SetStatusProgressResultAsync(taskId, status, progress, result, cancellationToken);
            await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
            _prevProgress = progress ?? 0;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Can't save progress");
        }
    }
}
