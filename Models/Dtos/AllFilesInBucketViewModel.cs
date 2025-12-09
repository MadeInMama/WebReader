namespace WebReader.Models.Dtos;

public class AllFilesInBucketViewModel
{
    public required string BucketId { get; init; }
    public required IEnumerable<AllFilesInBucketItem> Items { get; init; }
}

public class AllFilesInBucketItem
{
    public required string Name { get; init; }
    public required string CustomName { get; init; }
    public DateTimeOffset DateTime { get; init; }
    public ulong Size { get; init; }
    public string? Type { get; init; }
}
