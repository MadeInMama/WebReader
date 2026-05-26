using FluentResults;
using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.SyncDbWithS3;

public class RemoveBucketsThatNotExistsInDb(IServiceProvider services, ILogger<RemoveBucketsThatNotExistsInDb> logger)
    : IBackgroundTasked
{
    //TODO: progress
    public async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true, cancellationToken)).ToList();

        logger.LogTrace(
            $"{nameof(RemoveBucketsThatNotExistsInDb)}: Found {{count}} buckets in database", allBucketsInDb.Count);

        var allBucketsInS3 = (await minioService.ListBucketsAsync(cancellationToken)).ToList();

        logger.LogTrace(
            $"{nameof(RemoveBucketsThatNotExistsInDb)}: Found {{count}} buckets in s3", allBucketsInS3.Count);

        var tasks = new List<Task>();
        var namesRemoved = new List<string>();

        foreach (var bucketInS3 in allBucketsInS3)
            if (allBucketsInDb.FirstOrDefault(f => f.Name == bucketInS3.Name) == null)
            {
                namesRemoved.Add(bucketInS3.Name);
                tasks.Add(minioService.RemoveBucketAsync(bucketInS3.Name, cancellationToken));
            }

        await Task.WhenAll(tasks);

        return Result.Ok($"Removed buckets: {string.Join(", ", namesRemoved)}");
    }
}
