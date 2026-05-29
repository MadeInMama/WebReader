using System.Text.Json;

namespace WebReader.Models.Dtos.Item;

public class AllScheduledTasksItem
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedDate { get; init; }
    public DateTimeOffset UpdatedDate { get; init; }
    public DateTimeOffset HaveToStartAt { get; init; }
    public TaskType Type { get; init; }
    public sbyte Priority { get; init; }
    public TaskStatus Status { get; init; }
    public string? Result { get; init; }

    public decimal Progress { get; init; }

    public Guid? ScheduledTaskConfigId { get; init; }
    public TaskConfigCron Cron { get; init; }
    public JsonDocument Settings { get; init; }
}
