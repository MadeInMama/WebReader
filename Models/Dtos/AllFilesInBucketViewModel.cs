using Minio.DataModel;

namespace WebReader.Models.Dtos;

public class AllFilesInBucketViewModel
{
    public required string BucketId { get; init; }
    public required IEnumerable<Item> Files { get; init; }
}