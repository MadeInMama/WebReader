using WebReader.Models.Dtos.Item;

namespace WebReader.Models.Dtos;

public class AllFilesReadingViewModel
{
    public required IDictionary<AllFilesReadingItemKey, IEnumerable<AllFilesReadingItem>> Items { get; init; }
}
