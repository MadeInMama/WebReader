using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

public class BucketService(
    MinioService minioService,
    BucketRepository bucketRepository)
{
    public async Task RemoveBucketAsync(Bucket? bucket)
    {
        if (bucket == null) return;

        var dbTask = bucketRepository.DeleteAsync(bucket.Id);
        var s3Task = minioService.RemoveBucketAsync(bucket.Name);

        await Task.WhenAll(s3Task, dbTask);
    }
}
