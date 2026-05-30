using System.Text.Json;

namespace WebReader.Models.Entities;

public class ScheduledTaskConfig : BaseEntity
{
    public TaskType Type { get; set; }
    public sbyte DefaultPriority { get; set; }
    public TaskCron Cron { get; set; }
    public JsonDocument DefaultSettings { get; set; } = JsonDocument.Parse("{}");
    public bool IsActive { get; set; } = true;
}
