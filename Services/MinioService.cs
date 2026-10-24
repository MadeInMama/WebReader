using System.Xml.Linq;
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

    public async Task<(string ttsId, string ttsLang, string ttsText, string ttsType)> GetTtsTextByTtsIdAsync(
        string bucketId,
        string objectName, string ttsId)
    {
        (string? ttsId, string? ttsLang, string? ttsText, string? ttsType) res = (null, null, null, null);

        await minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketId)
            .WithObject(objectName)
            .WithCallbackStream(f =>
            {
                try
                {
                    var targetElement = XDocument.Load(f).Descendants()
                        .FirstOrDefault(el => el.Attribute("tts-id")?.Value == ttsId);

                    if (targetElement != null)
                        res = (targetElement.Attribute("tts-id")?.Value,
                            targetElement.Attribute("tts-lang")?.Value,
                            targetElement.Value,
                            targetElement.Attribute("tts-type")?.Value);
                }
                catch (Exception)
                {
                    // ignored
                }
            }));

        if (res is { ttsId: null } or { ttsLang: null } or { ttsText: null })
            throw new Exception($"Failed to get {ttsId} from {bucketId}/{objectName}.");

        return res!;
    }

    public async Task<string> GetCoverFileUrlAsync(string objectName)
    {
        return await GetFileUrlAsync(StaticNames.CoversBucketName, objectName);
    }

    public async Task<IEnumerable<Bucket>> ListBucketsAsync(CancellationToken cancellationToken)
    {
        return (await minioClient.ListBucketsAsync(cancellationToken)).Buckets;
    }

    public async Task<IEnumerable<Item>> ListObjectsAsync(string bucketName, CancellationToken cancellationToken)
    {
        return await minioClient
            .ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(bucketName), cancellationToken)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateBucketAsync(string bucketName, CancellationToken cancellationToken)
    {
        await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), cancellationToken);
    }

    public async Task RemoveBucketAsync(string? bucketName, CancellationToken cancellationToken)
    {
        if (bucketName == null) return;

        var observable = minioClient.ListObjectsEnumAsync(new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithRecursive(true), cancellationToken);

        var objNames = new List<string>();

        await foreach (var item in observable)
            if (item != null)
                objNames.Add(item.Key);

        await RemoveObjectsAsync(bucketName, objNames, cancellationToken);
        await minioClient.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucketName), cancellationToken);
    }

    public async Task RemoveObjectsAsync(string? bucketName, List<string>? objectNames,
        CancellationToken cancellationToken)
    {
        if (bucketName == null) return;
        if (objectNames == null || objectNames.Count == 0) return;

        await minioClient.RemoveObjectsAsync(new RemoveObjectsArgs()
            .WithBucket(bucketName)
            .WithObjects(objectNames), cancellationToken);
    }

    public async Task<bool> ObjectExistsAsync(string bucketName, string objectName, CancellationToken cancellationToken)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await minioClient.StatObjectAsync(args, cancellationToken);

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

    public async Task<bool> UploadObjectAsync(string bucketName, Stream stream, string fileName, string contentType,
        CancellationToken cancellationToken)
    {
        if (await ObjectExistsAsync(bucketName, fileName, cancellationToken)) return false;

        stream.Position = 0;

        var res = await minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(fileName)
            .WithStreamData(stream)
            .WithContentType(contentType)
            .WithObjectSize(stream.Length), cancellationToken);

        stream.Close();
        await stream.DisposeAsync();

        return res != null && res.ResponseStatusCode.IsSuccessStatusCode();
    }

    public async Task<bool> UploadCoverAsync(Stream stream, string fileName, string contentType,
        CancellationToken cancellationToken)
    {
        return await UploadObjectAsync(StaticNames.CoversBucketName, stream, fileName, contentType, cancellationToken);
    }

    public async Task<bool> UploadTtsAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        return await UploadObjectAsync(StaticNames.TtsBucketName, stream, fileName, "audio/ogg", cancellationToken);
    }
}
