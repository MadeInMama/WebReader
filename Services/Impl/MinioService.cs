using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;

namespace WebReader.Services.Impl;

public class MinioService(IMinioClient minioClient) : IMinioService
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
}