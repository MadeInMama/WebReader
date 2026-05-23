using Minio.DataModel;
using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;
using Bucket = WebReader.Models.Entities.Bucket;

namespace WebReader.Background.SyncDbWithS3;

public class UpdateBucketData(IServiceProvider services, ILogger<UpdateBucketData> logger) : IBackgroundTasked
{
    public async Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Start {nameof(UpdateBucketData)}");

        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true)).ToList();

        var toSave = new List<Bucket>();

        foreach (var bucketInDb in allBucketsInDb)
        {
            var size = (await minioService.ListObjectsAsync(bucketInDb.Name)).ToList()
                .Aggregate<Item, ulong>(0, (current, s) => current + s.Size);

            if (size == bucketInDb.Size) continue;

            bucketInDb.Size = size;
            toSave.Add(bucketInDb);
        }

        if (toSave.Count != 0) bucketRepository.UpdateAll(toSave);

        logger.LogInformation($"{nameof(UpdateBucketData)}: Total update count {{updated}}",
            await bucketRepository.SaveChangesAsync());

        logger.LogInformation($"Finished {nameof(UpdateBucketData)}");
    }
}
