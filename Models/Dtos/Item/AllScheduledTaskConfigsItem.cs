using System.Text.Json;

namespace WebReader.Models.Dtos.Item;

public class AllScheduledTaskConfigsItem
{
    public required TaskType Type { get; init; }
    public required sbyte Priority { get; init; }
    public required JsonElement Settings { get; init; }
}
