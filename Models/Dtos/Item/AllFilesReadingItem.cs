namespace WebReader.Models.Dtos.Item;

public class AllFilesReadingItem
{
    public required Guid ReadingId { get; init; }
    public required Guid FileId { get; init; }
    public required string CustomName { get; init; }
    public string? CurrentPartName { get; init; }
    public DateTimeOffset DateTime { get; init; }
    public ulong Size { get; init; }
    public int Page { get; init; }
}
