using FluentResults;
using WebReader.Models.Entities;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background;

public interface IBackgroundTasked
{
    public Task PreExecuteAsync(CancellationToken cancellationToken);

    public Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken);

    public Task PostExecuteAsync(CancellationToken cancellationToken);

    public Task UpdateProgress(Guid taskId, TaskStatus status, decimal? progress, string? result,
        CancellationToken cancellationToken);
}
