using WebReader.Models.Dtos.Item;

namespace WebReader.Models.Dtos;

public class AllScheduledTaskConfigsViewModel
{
    public required IEnumerable<AllScheduledTaskConfigsItem> Items { get; init; }
}
