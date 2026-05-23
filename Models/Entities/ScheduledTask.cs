namespace WebReader.Models.Entities;

public class ScheduledTask : BaseEntity
{
    public TaskType Type { get; init; }
    public sbyte Priority { get; init; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public string? ErrorMessage { get; set; }

    //TODO: Progress

    public Guid? ScheduledTaskConfigId { get; init; }
    public ScheduledTaskConfig? ScheduledTaskConfig { get; init; }
}
