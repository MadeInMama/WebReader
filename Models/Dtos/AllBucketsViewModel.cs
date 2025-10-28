using Minio.DataModel;

namespace WebReader.Models.Dtos;

public class AllBucketsViewModel
{
    public required IEnumerable<Bucket> Buckets { get; init; }
}