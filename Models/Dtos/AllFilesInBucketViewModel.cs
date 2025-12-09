namespace WebReader.Models.Dtos;

public class AllFilesInBucketViewModel
{
    public required string BucketId { get; init; }
    public required IEnumerable<AllFilesInBucketItem> Items { get; init; }
}

public class AllFilesInBucketItem
{
    public required string Name { get; set; }
    public required string CustomName { get; set; }
    public DateTime DateTime { get; set; }
    public ulong Size { get; set; }
    public string? Type { get; set; }
}
