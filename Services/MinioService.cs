using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using WebReader.Helpers;

namespace WebReader.Services;

public class MinioService(IMinioClient minioClient)
{
    public async Task<string> GetFileUrlAsync(string bucketId, string objectName)
    {
        var url = await minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucketId)
            .WithObject(objectName)
            .WithExpiry(3600));

        return url ?? throw new Exception("Failed to get url");
    }

    public async Task<IEnumerable<Bucket>> ListBucketsAsync()
    {
        return (await minioClient.ListBucketsAsync()).Buckets;
    }

    public async Task<IEnumerable<Item>> ListObjectsAsync(string bucketName)
    {
        return await minioClient.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(bucketName)).ToListAsync();
    }

    public async Task CreateBucketAsync(string bucketName)
    {
        await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
    }

    public async Task RemoveBucketAsync(string? bucketName)
    {
        if (bucketName == null) return;

        var observable = minioClient.ListObjectsEnumAsync(new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithRecursive(true));

        var objNames = new List<string>();

        await foreach (var item in observable)
            if (item != null)
                objNames.Add(item.Key);

        await RemoveObjectsAsync(bucketName, objNames);
        await minioClient.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucketName));
    }

    public async Task RemoveObjectsAsync(string? bucketName, List<string>? objectNames)
    {
        if (bucketName == null) return;
        if (objectNames == null || objectNames.Count == 0) return;

        await minioClient.RemoveObjectsAsync(new RemoveObjectsArgs()
            .WithBucket(bucketName)
            .WithObjects(objectNames));
    }

    public async Task<bool> ObjectExistsAsync(string bucketName, string objectName)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await minioClient.StatObjectAsync(args);

            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (BucketNotFoundException)
        {
            return false;
        }
    }

    public async Task<bool> UploadObjectAsync(string bucketName, IFormFile file)
    {
        if (await ObjectExistsAsync(bucketName, file.FileName)) return false;

        var res = await minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(file.FileName)
            .WithStreamData(file.OpenReadStream())
            .WithContentType(file.ContentType)
            .WithObjectSize(file.Length));

        return res != null && res.ResponseStatusCode.IsSuccessStatusCode();
    }
}
