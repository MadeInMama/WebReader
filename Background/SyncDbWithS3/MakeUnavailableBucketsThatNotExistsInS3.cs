using FluentResults;
using Microsoft.AspNetCore.SignalR;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.SyncDbWithS3;

public class MakeUnavailableBucketsThatNotExistsInS3(
    IServiceProvider services,
    ScheduledTaskRepository taskRepository,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ILogger<MakeUnavailableBucketsThatNotExistsInS3> logger)
    : AbstractBackgroundTasked<MakeUnavailableBucketsThatNotExistsInS3>(taskRepository, scheduledTaskHubContext, logger)
{
    public override async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true, cancellationToken)).ToList();

        logger.LogTrace(
            $"{nameof(MakeUnavailableBucketsThatNotExistsInS3)}: Found {{count}} buckets in database",
            allBucketsInDb.Count);

        var allBucketsInS3 = (await minioService.ListBucketsAsync(cancellationToken)).ToList();

        logger.LogTrace(
            $"{nameof(MakeUnavailableBucketsThatNotExistsInS3)}: Found {{count}} buckets in s3", allBucketsInS3.Count);

        var toSave = new List<Bucket>();

        var totalCount = allBucketsInDb.Count;
        var currentCount = 0;

        foreach (var bucketInDb in allBucketsInDb)
        {
            var isAvailable = allBucketsInS3.Exists(f => f.Name.Equals(bucketInDb.Name));

            if (bucketInDb.IsAvailable == isAvailable) continue;

            logger.LogTrace(
                $"{nameof(RemoveBucketsThatNotExistsInDb)}: Bucket {{bucketName}} availability will be set to {{}}",
                bucketInDb.Name, isAvailable);

            bucketInDb.IsAvailable = isAvailable;

            toSave.Add(bucketInDb);

            await UpdateProgress(task.Id, TaskStatus.InProgress, new decimal(++currentCount) / totalCount, null,
                cancellationToken);
        }

        if (toSave.Count != 0) bucketRepository.AttachAll(toSave);

        var result = await bucketRepository.SaveChangesAsync(cancellationToken);

        logger.LogTrace($"{nameof(MakeUnavailableBucketsThatNotExistsInS3)}: Total update count: {{updated}}",
            result);

        return Result.Ok($"Update count: {result}");
    }
}
