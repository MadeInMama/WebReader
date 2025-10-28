using Minio.DataModel;

namespace WebReader.Models.Dtos;

public class AllFilesReadingViewModel
{
    public required IDictionary<string, IEnumerable<Item>> BucketFiles { get; init; }
}