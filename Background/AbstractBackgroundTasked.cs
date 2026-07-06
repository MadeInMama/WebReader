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
    public abstract Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken);

    public virtual async Task UpdateProgress(Guid taskId, TaskStatus status, decimal? progress, string? result,
        CancellationToken cancellationToken)
    {
        try
        {
            await taskRepository.SetStatusProgressResultAsync(taskId, status, progress, result, cancellationToken);
            await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogWarning("Can't save progress: {}", e.Message);
        }
    }
}
