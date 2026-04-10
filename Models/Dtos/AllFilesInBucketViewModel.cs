using WebReader.Models.Dtos.Item;

namespace WebReader.Models.Dtos;

public class AllFilesInBucketViewModel
{
    public required Guid Id { get; init; }
    public required string BucketName { get; init; }
    public required bool IsBelongsToUser { get; init; }
    public required IEnumerable<AllFilesInBucketItem> Items { get; init; }
}
