namespace WebReader.Models.Dtos;

public class AllFilesReadingViewModel
{
    public required IDictionary<string, IEnumerable<AllFilesReadingItem>> Items { get; init; }
}

public class AllFilesReadingItem
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string CustomName { get; init; }
    public DateTimeOffset DateTime { get; init; }
    public ulong Size { get; init; }
    public int Page { get; init; }
}
