using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Services;

public class FileService(
    FileRepository fileRepository,
    ScheduledTaskRepository scheduledTaskRepository,
    ILogger<FileService> logger)
{
    public async Task DeleteFilesAsync(List<Guid> guids, CancellationToken cancellationToken)
    {
        logger.LogInformation("Deleting files with ids: ({count}, {guids})", guids.Count, string.Join(", ", guids));

        foreach (var guid in guids.Index())
        {
            logger.LogTrace("Deleting file with id: ({index}, {guid})", guid.Index + 1, guid.Item);

            var file = await fileRepository.FirstOrDefaultAsync(f => f.Id == guid.Item, null, cancellationToken, false,
                f => f.Bucket);

            if (file == null) continue;

            var prevFile =
                await fileRepository.FirstOrDefaultAsync(f => f.NextPartId == guid.Item, null, cancellationToken,
                    false);

            if (prevFile != null)
            {
                if (file.NextPartId.HasValue)
                    prevFile.NextPartId = file.NextPartId.Value;
                else
                    prevFile.NextPartId = null;
            }

            await fileRepository.SaveChangesAsync(cancellationToken);

            await fileRepository.DeleteAsync(guid.Item, cancellationToken);
        }

        var task = await scheduledTaskRepository.FirstOrDefaultAsync(
            f => TaskType.RemoveFilesThatNotExistsInDb == f.Type &&
                 TaskStatus.Pending == f.Status &&
                 f.HaveToStartAt < DateTimeOffset.UtcNow,
            null, cancellationToken, true);

        if (task == null)
        {
            await scheduledTaskRepository.AddRangeAsync([
                new ScheduledTask
                {
                    Type = TaskType.RemoveFilesThatNotExistsInDb,
                    Priority = sbyte.MaxValue,
                    Cron = TaskCron.Manually,
                    HaveToStartAt = DateTimeOffset.UtcNow
                },
                new ScheduledTask
                {
                    Type = TaskType.RemoveTtsForFilesThatNotExistsInDb,
                    Priority = sbyte.MaxValue - 1,
                    Cron = TaskCron.Manually,
                    HaveToStartAt = DateTimeOffset.UtcNow
                }
            ], cancellationToken);
            await scheduledTaskRepository.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Deleting files done");
    }
}
