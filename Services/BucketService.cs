using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

public class BucketService(MinioService minioService, BucketRepository bucketRepository)
{
    public async Task CreatePersonalBucketAsync(CustomUser entity)
    {
        var bucketName = $"personal-{entity.Id}";

        var bucket = minioService.CreateBucketAsync(bucketName);
        var bucket1 = bucketRepository.AddAsync(new Bucket
        {
            Name = bucketName,
            CustomName = bucketName,
            IsAvailable = true,
            IsHidden = false,
            UserId = entity.Id,
            User = entity
        });

        await Task.WhenAll(bucket, bucket1);
    }
}
