using FluentResults;
using WebReader.Models.Entities;

namespace WebReader.Background;

public interface IBackgroundTasked
{
    //TODO: before exec method
    Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken);
    //TODO: after exec method
}
