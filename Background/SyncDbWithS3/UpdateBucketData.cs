using FluentResults;
using Minio.DataModel;
using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;
using Bucket = WebReader.Models.Entities.Bucket;

namespace WebReader.Background.SyncDbWithS3;

public class UpdateBucketData(IServiceProvider services, ILogger<UpdateBucketData> logger) : IBackgroundTasked
{
    //TODO: progress
    public async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true, cancellationToken)).ToList();

        var toSave = new List<Bucket>();

        foreach (var bucketInDb in allBucketsInDb)
        {
            var size = (await minioService.ListObjectsAsync(bucketInDb.Name, cancellationToken)).ToList()
                .Aggregate<Item, ulong>(0, (current, s) => current + s.Size);

            if (size == bucketInDb.Size) continue;

            bucketInDb.Size = size;
            toSave.Add(bucketInDb);
        }

        if (toSave.Count != 0) bucketRepository.UpdateAll(toSave);

        var result = await bucketRepository.SaveChangesAsync(cancellationToken);

        logger.LogTrace($"{nameof(UpdateBucketData)}: Total update count {{updated}}", result);

        return Result.Ok($"Update count: {result}");
    }
}
