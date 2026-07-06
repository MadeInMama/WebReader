using FluentResults;
using Microsoft.AspNetCore.SignalR;
using Minio.DataModel;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using Bucket = WebReader.Models.Entities.Bucket;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.SyncDbWithS3;

public class UpdateBucketData(
    IServiceProvider services,
    ScheduledTaskRepository taskRepository,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ILogger<UpdateBucketData> logger)
    : AbstractBackgroundTasked<UpdateBucketData>(taskRepository, scheduledTaskHubContext, logger)
{
    public override async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true, cancellationToken)).ToList();

        var toSave = new List<Bucket>();

        var totalCount = allBucketsInDb.Count;
        var currentCount = 0;

        foreach (var bucketInDb in allBucketsInDb)
        {
            var size = (await minioService.ListObjectsAsync(bucketInDb.Name, cancellationToken)).ToList()
                .Aggregate<Item, ulong>(0, (current, s) => current + s.Size);

            if (size != bucketInDb.Size)
            {
                bucketInDb.Size = size;
                toSave.Add(bucketInDb);
            }

            await UpdateProgress(task.Id, TaskStatus.InProgress, new decimal(++currentCount) / totalCount, null,
                cancellationToken);
        }

        if (toSave.Count != 0) bucketRepository.AttachAll(toSave);

        var result = await bucketRepository.SaveChangesAsync(cancellationToken);

        logger.LogTrace($"{nameof(UpdateBucketData)}: Total update count {{updated}}", result);

        return Result.Ok($"Update count: {result}");
    }
}
