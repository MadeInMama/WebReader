namespace WebReader.Models.Dtos.Item;

public class AllScheduledTaskConfigsItem
{
    public required Guid Id { get; init; }
    public required TaskType Type { get; init; }
}
