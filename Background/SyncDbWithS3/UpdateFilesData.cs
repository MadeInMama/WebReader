using FluentResults;
using Microsoft.AspNetCore.SignalR;
using Minio.DataModel;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using File = WebReader.Models.Entities.File;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.SyncDbWithS3;

public class UpdateFilesData(
    IServiceProvider services,
    ScheduledTaskRepository taskRepository,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ILogger<UpdateFilesData> logger)
    : AbstractBackgroundTasked<UpdateFilesData>(taskRepository, scheduledTaskHubContext, logger)
{
    public override async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var fileRepository = scope.ServiceProvider.GetRequiredService<FileRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allFilesInDb = (await fileRepository.AllAsync(f => true, cancellationToken, false, f => f.Bucket)).ToList();
        var allBucketsInS3 = (await minioService.ListBucketsAsync(cancellationToken)).ToList();
        Dictionary<string, Task<List<Item>>> allFilesInS3 =
            allBucketsInS3.ToDictionary(f => f.Name,
                async f => (await minioService.ListObjectsAsync(f.Name, cancellationToken)).ToList());

        await Task.WhenAll(allFilesInS3.Values);

        var toSave = new List<File>();

        var totalCount = allFilesInDb.Count;
        var currentCount = 0;

        foreach (var fileInDb in allFilesInDb)
        {
            var isChanged = false;

            if (allFilesInS3.TryGetValue(fileInDb.Bucket.Name, out var filesInS3))
            {
                var fileInS3 = filesInS3.Result.FirstOrDefault(f => f.Key.Equals(fileInDb.Name));

                if (fileInS3 != null)
                {
                    if (fileInS3.Key.TryGetFileType(out var fileType))
                    {
                        if (!fileInDb.IsAvailable ||
                            fileInDb.Size != fileInS3.Size ||
                            fileInDb.Type != fileType)
                            isChanged = true;

                        fileInDb.IsAvailable = true;
                        fileInDb.Size = fileInS3.Size;
                        fileInDb.Type = fileType;
                    }
                    else
                    {
                        if (fileInDb.IsAvailable)
                            isChanged = true;

                        fileInDb.IsAvailable = false;
                    }
                }
                else
                {
                    if (fileInDb.IsAvailable)
                        isChanged = true;

                    fileInDb.IsAvailable = false;
                }
            }
            else
            {
                if (fileInDb.IsAvailable)
                    isChanged = true;

                fileInDb.IsAvailable = false;
            }

            if (isChanged) toSave.Add(fileInDb);

            await UpdateProgress(task.Id, TaskStatus.InProgress, new decimal(++currentCount) / totalCount, null,
                cancellationToken);
        }

        if (toSave.Count != 0) fileRepository.AttachAll(toSave);

        var result = await fileRepository.SaveChangesAsync(cancellationToken);
        logger.LogTrace($"{nameof(UpdateFilesData)}: Total update count {{updated}}", result);

        return Result.Ok($"Update count: {result}");
    }
}
