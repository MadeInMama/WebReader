using WebReader.Models.Dtos.Item;

namespace WebReader.Models.Dtos;

public class AllBucketsViewModel
{
    public required IEnumerable<AllBucketsItem> Items { get; init; }
}
