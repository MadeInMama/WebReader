using WebReader.Models.Dtos.Item;

namespace WebReader.Models.Dtos;

public class AllFilesInBucketViewModel
{
    public required Guid Id { get; init; }
    public required string BucketName { get; init; }
    public required bool IsBelongsToUser { get; init; }
    public required IEnumerable<AllFilesInBucketItem> Items { get; init; }

    public required int TotalCount { get; init; }
    public required int PageCount { get; init; }
    public required int PageIndex { get; init; }
    public required bool AscDirection { get; init; }
    public required string OrderBy { get; init; }
}
