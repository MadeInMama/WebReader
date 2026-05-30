namespace WebReader.Models.Dtos;

public class CreateTaskRequest
{
    public required TaskType Type { get; init; }
    public required DateTimeOffset HaveToStartAt { get; init; }
    public required sbyte Priority { get; init; }
    public required string Settings { get; init; }
}
