using WebReader.Models.Dtos.Item;

namespace WebReader.Models.Dtos;

public class AllFilesByBucketViewModel
{
    public required IDictionary<AllFilesByBucketItemKey, List<AllFilesByBucketItem>> Items { get; init; }
}
