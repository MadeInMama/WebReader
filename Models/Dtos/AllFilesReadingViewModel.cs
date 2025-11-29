namespace WebReader.Models.Dtos;

public class AllFilesReadingViewModel
{
    public required IDictionary<string, IEnumerable<AllFilesReadingItem>> Items { get; init; }
}

public class AllFilesReadingItem
{
    public required string Name { get; set; }
    public required string CustomName { get; set; }
    public DateTime DateTime { get; set; }
    public ulong Size { get; set; }
    public int Page { get; set; }
}
