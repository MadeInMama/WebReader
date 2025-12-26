namespace WebReader.Models.Dtos;

public class AllFilesInBucketViewModel
{
    public required Guid Id { get; init; }
    public required string BucketName { get; init; }
    public required bool IsBelongsToUser { get; init; }
    public required IEnumerable<AllFilesInBucketItem> Items { get; init; }
}

public class AllFilesInBucketItem
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public DateTimeOffset DateTime { get; init; }
    public ulong Size { get; init; }
    public FileType Type { get; init; }
    public bool IsReading { get; init; }
}
