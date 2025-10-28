using Minio.DataModel;

namespace WebReader.Services;

public interface IMinioService
{
    public Task<string> GetFileUrlAsync(string bucketId, string objectName);
    public Task<IEnumerable<Bucket>> ListBucketsAsync();

    public Task<IEnumerable<Item>> ListObjectsAsync(string bucketName);
}