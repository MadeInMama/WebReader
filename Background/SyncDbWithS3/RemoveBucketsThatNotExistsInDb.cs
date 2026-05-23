using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.SyncDbWithS3;

public class RemoveBucketsThatNotExistsInDb(IServiceProvider services, ILogger<RemoveBucketsThatNotExistsInDb> logger)
    : IBackgroundTasked
{
    public async Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Start {nameof(RemoveBucketsThatNotExistsInDb)}");

        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true)).ToList();

        logger.LogInformation(
            $"{nameof(RemoveBucketsThatNotExistsInDb)}: Found {{count}} buckets in database", allBucketsInDb.Count);

        var allBucketsInS3 = (await minioService.ListBucketsAsync()).ToList();

        logger.LogInformation(
            $"{nameof(RemoveBucketsThatNotExistsInDb)}: Found {{count}} buckets in s3", allBucketsInS3.Count);

        var tasks = new List<Task>();

        foreach (var bucketInS3 in allBucketsInS3)
            if (allBucketsInDb.FirstOrDefault(f => f.Name == bucketInS3.Name) == null)
            {
                logger.LogInformation(
                    $"{nameof(RemoveBucketsThatNotExistsInDb)}: Bucket {{bucketName}} will be removed",
                    bucketInS3.Name);
                tasks.Add(minioService.RemoveBucketAsync(bucketInS3.Name));
            }

        await Task.WhenAll(tasks);

        logger.LogInformation($"Finished {nameof(RemoveBucketsThatNotExistsInDb)}");
    }
}
