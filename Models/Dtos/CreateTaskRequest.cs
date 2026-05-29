namespace WebReader.Models.Dtos;

public class CreateTaskRequest
{
    public required Guid ScheduledTaskConfigId { get; init; }
    public required DateTimeOffset HaveToStartAt { get; init; }
    public required sbyte Priority { get; init; }
}
