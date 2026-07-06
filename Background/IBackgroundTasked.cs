using FluentResults;
using WebReader.Models.Entities;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background;

public interface IBackgroundTasked
{
    //TODO: before exec method
    Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken);
    //TODO: after exec method

    Task UpdateProgress(Guid taskId, TaskStatus status, decimal? progress, string? result,
        CancellationToken cancellationToken);
}
