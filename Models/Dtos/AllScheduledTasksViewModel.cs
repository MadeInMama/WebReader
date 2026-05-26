using WebReader.Models.Dtos.Item;

namespace WebReader.Models.Dtos;

public class AllScheduledTasksViewModel
{
    public required IEnumerable<AllScheduledTasksItem> Items { get; init; }
}
