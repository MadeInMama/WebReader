using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;

namespace WebReader.Services;

public class MinioService(IMinioClient minioClient)
{
    public async Task<string> GetFileUrlAsync(string bucketId, string objectName)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketId)
            .WithObject(objectName)
            .WithExpiry(3600);

        var url = await minioClient.PresignedGetObjectAsync(args)
            .ConfigureAwait(false);

        return url ?? throw new Exception("Failed to get url");
    }

    public async Task<IEnumerable<Bucket>> ListBucketsAsync()
    {
        return (await minioClient.ListBucketsAsync().ConfigureAwait(false)).Buckets;
    }

    public async Task<IEnumerable<Item>> ListObjectsAsync(string bucketName)
    {
        return await minioClient.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(bucketName)).ToListAsync();
    }

    public async Task CreateBucketAsync(string bucketName)
    {
        await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
    }

    public async Task RemoveBucketAsync(string bucketName)
    {
        var observable = minioClient.ListObjectsEnumAsync(new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithRecursive(true));

        var tasks = new List<Task>();

        await foreach (var item in observable)
            if (item != null)
                tasks.Add(minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(item.Key)));

        await Task.WhenAll(tasks);

        await minioClient.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucketName));
    }
}
