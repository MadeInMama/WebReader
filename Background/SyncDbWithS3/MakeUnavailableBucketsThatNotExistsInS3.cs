using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.SyncDbWithS3;

public class MakeUnavailableBucketsThatNotExistsInS3(
    IServiceProvider services,
    ILogger<MakeUnavailableBucketsThatNotExistsInS3> logger)
    : IBackgroundTasked
{
    public async Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Start {nameof(MakeUnavailableBucketsThatNotExistsInS3)}");

        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true)).ToList();

        logger.LogInformation(
            $"{nameof(MakeUnavailableBucketsThatNotExistsInS3)}: Found {{count}} buckets in database",
            allBucketsInDb.Count);

        var allBucketsInS3 = (await minioService.ListBucketsAsync()).ToList();

        logger.LogInformation(
            $"{nameof(MakeUnavailableBucketsThatNotExistsInS3)}: Found {{count}} buckets in s3", allBucketsInS3.Count);

        var toSave = new List<Bucket>();

        foreach (var bucketInDb in allBucketsInDb)
        {
            var isAvailable = allBucketsInS3.Exists(f => f.Name.Equals(bucketInDb.Name));

            if (bucketInDb.IsAvailable == isAvailable) continue;

            logger.LogInformation(
                $"{nameof(RemoveBucketsThatNotExistsInDb)}: Bucket {{bucketName}} availability will be set to {{}}",
                bucketInDb.Name, isAvailable);

            bucketInDb.IsAvailable = isAvailable;

            toSave.Add(bucketInDb);
        }

        if (toSave.Count != 0) bucketRepository.UpdateAll(toSave);

        logger.LogInformation($"{nameof(MakeUnavailableBucketsThatNotExistsInS3)}: Total update count {{updated}}",
            await bucketRepository.SaveChangesAsync());

        logger.LogInformation($"Finished {nameof(MakeUnavailableBucketsThatNotExistsInS3)}");
    }
}
