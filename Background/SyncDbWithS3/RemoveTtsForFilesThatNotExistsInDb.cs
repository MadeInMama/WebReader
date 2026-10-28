using FluentResults;
using Microsoft.AspNetCore.SignalR;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.SyncDbWithS3;

public class RemoveTtsForFilesThatNotExistsInDb(
    IServiceProvider services,
    ScheduledTaskRepository taskRepository,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ILogger<RemoveTtsForFilesThatNotExistsInDb> logger)
    : AbstractBackgroundTasked<RemoveTtsForFilesThatNotExistsInDb>(taskRepository, scheduledTaskHubContext, logger)
{
    public override async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var fileRepository = scope.ServiceProvider.GetRequiredService<FileRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var filesWithTtsInDb =
            fileRepository.AllAsync(f =>
                    f.Bucket!.IsAvailable &&
                    !f.Bucket.IsSystem &&
                    f.IsAvailable &&
                    f.Type == FileType.Fb2,
                cancellationToken,
                false,
                null,
                f => f.Bucket);
        var ttsFilesInS3 = minioService.ListObjectsAsync("tts", cancellationToken);

        await Task.WhenAll(filesWithTtsInDb, ttsFilesInS3);

        var ttsFilesInDbFolders = filesWithTtsInDb.Result.Select(f => f.Name.Split(".")[0]).Distinct().ToList();
        var ttsFilesInS3Folders = new Dictionary<string, List<string>>();
        foreach (var item in ttsFilesInS3.Result)
        {
            if (item.IsDir) continue;

            var key = item.Key;
            var folderName = key.Split("/")[0];

            ttsFilesInS3Folders.AddOrAppend(folderName, key);
        }

        var tasks = new List<Task>();
        var namesRemoved = new List<string>();

        var totalCount = ttsFilesInS3Folders.Values.Sum(list => list.Count);
        var currentCount = 0;

        foreach (var ttsFilesInS3Folder in ttsFilesInS3Folders)
        {
            if (!ttsFilesInDbFolders.Contains(ttsFilesInS3Folder.Key))
            {
                namesRemoved.Add(ttsFilesInS3Folder.Key);
                tasks.Add(minioService.RemoveObjectsAsync("tts", ttsFilesInS3Folder.Value, cancellationToken));
            }

            await UpdateProgress(task.Id, TaskStatus.InProgress, new decimal(++currentCount) / totalCount, null,
                cancellationToken);
        }

        await Task.WhenAll(tasks);

        var res = string.Join(", ", namesRemoved);

        return Result.Ok(string.IsNullOrWhiteSpace(res) ? "No removed folders" : $"Removed folders: {res}");
    }
}
