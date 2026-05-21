namespace WebReader.Models.Dtos.Item;

public class AllFilesInBucketItem
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public DateTimeOffset DateTime { get; init; }
    public ulong Size { get; init; }
    public string? Type { get; init; }
    public bool IsReading { get; init; }
    public bool IsParted { get; init; }
    public bool IsDone { get; init; }
    public string? CurrentPartName { get; init; }
    public int TotalCount { get; set; }
    public ulong TotalSize { get; set; }
}
