using FluentResults;
using Microsoft.AspNetCore.SignalR;
using WebReader.Helpers;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.SyncDbWithS3;

public class RemoveFilesThatNotExistsInDb(
    IServiceProvider services,
    ScheduledTaskRepository taskRepository,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ILogger<RemoveFilesThatNotExistsInDb> logger)
    : AbstractBackgroundTasked<RemoveFilesThatNotExistsInDb>(taskRepository, scheduledTaskHubContext, logger)
{
    public override async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var fileRepository = scope.ServiceProvider.GetRequiredService<FileRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allFilesInDb = (await fileRepository.AllAsync(f => true, cancellationToken, true, f => f.Bucket)).ToList();
        var allBucketsInS3 = (await minioService.ListBucketsAsync(cancellationToken)).ToList();
        var allFilesInS3 =
            allBucketsInS3.ToDictionary(f => f.Name,
                async f => await minioService.ListObjectsAsync(f.Name, cancellationToken));

        await Task.WhenAll(allFilesInS3.Values);

        var objToRemove = new Dictionary<string, List<string>>();

        foreach (var (bucketName, filesInS3) in allFilesInS3)
        foreach (var fileInS3 in filesInS3.Result)
            if (allFilesInDb.FirstOrDefault(f => f.Bucket.Name == bucketName && f.Name == fileInS3.Key) == null)
                objToRemove.AddOrAppend(bucketName, fileInS3.Key);

        var tasks = new List<Task>();
        var namesRemoved = new List<string>();

        var totalCount = objToRemove.Count;
        var currentCount = 0;

        foreach (var (bucketName, objectNames) in objToRemove)
        {
            namesRemoved.Add($"{bucketName}/({string.Join(",", objectNames)})");
            tasks.Add(minioService.RemoveObjectsAsync(bucketName, objectNames, cancellationToken));

            await UpdateProgress(task.Id, TaskStatus.InProgress, new decimal(++currentCount) / totalCount, null,
                cancellationToken);
        }

        await Task.WhenAll(tasks);

        return Result.Ok($"Removed files: {string.Join(", ", namesRemoved)}");
    }
}
