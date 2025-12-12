using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

public class BucketService(
    MinioService minioService,
    BucketRepository bucketRepository)
{
    public async Task CreatePersonalBucketAsync(CustomUser user)
    {
        var bucketName = $"personal-{user.Id}";

        var bucket = minioService.CreateBucketAsync(bucketName);
        var bucket1 = bucketRepository.AddAsync(new Bucket
        {
            Name = bucketName,
            CustomName = bucketName,
            IsAvailable = true,
            IsHidden = false,
            UserId = user.Id,
            User = user
        });

        await Task.WhenAll(bucket, bucket1);
    }

    public async Task RemoveBucketAsync(Bucket? bucket)
    {
        if (bucket == null) return;

        var dbTask = bucketRepository.DeleteAsync(bucket.Id);
        var s3Task = minioService.RemoveBucketAsync(bucket.Name);

        await Task.WhenAll(s3Task, dbTask);
    }
}
