using FluentResults;
using WebReader.Models.Entities;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.Delete;

public class ClearUserTgLinks(UserTgLinkRepository userTgLinkRepository, ILogger<ClearUserTgLinks> logger)
    : IBackgroundTasked
{
    public async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        var result = await userTgLinkRepository.DeleteAllUsedOrExpiredAsync(cancellationToken);

        logger.LogTrace($"{nameof(ClearUserTgLinks)}: Deleted rows count: {{}}", result);

        return Result.Ok($"Deleted count: {result}");
    }

    public Task UpdateProgress(Guid taskId, TaskStatus status, decimal? progress, string? result,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Skipped");
        return Task.CompletedTask;
    }
}
