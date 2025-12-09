namespace WebReader.Models.Dtos;

public class AllBucketsViewModel
{
    public required IEnumerable<AllBucketsItem> Items { get; init; }
}

public class AllBucketsItem
{
    public required string Name { get; set; }
    public required string CustomName { get; set; }
    public DateTime DateTime { get; set; }
}
