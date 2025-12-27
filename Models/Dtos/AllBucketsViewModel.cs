namespace WebReader.Models.Dtos;

public class AllBucketsViewModel
{
    public required IEnumerable<AllBucketsItem> Items { get; init; }
}

public class AllBucketsItem
{
    public required Guid Id { get; init; }
    public required string CustomName { get; init; }
    public DateTimeOffset DateTime { get; init; }
    public ulong Size { get; init; }
}
