using System.Text.Json;

namespace WebReader.Models.Entities;

public class ScheduledTaskConfig : BaseEntity
{
    public TaskType Type { get; init; }
    public sbyte DefaultPriority { get; init; }
    public TaskConfigCron Cron { get; init; }
    public JsonDocument Settings { get; set; } = JsonDocument.Parse("{}");
}
