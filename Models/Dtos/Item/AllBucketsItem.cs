namespace WebReader.Models.Dtos.Item;

public class AllBucketsItem
{
    public required Guid Id { get; init; }
    public required string CustomName { get; init; }
    public DateTimeOffset DateTime { get; init; }
    public ulong Size { get; init; }
}
