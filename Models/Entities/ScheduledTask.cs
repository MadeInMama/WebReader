using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace WebReader.Models.Entities;

public class ScheduledTask : BaseEntity
{
    public required TaskType Type { get; init; }
    public required sbyte Priority { get; init; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public string? Result { get; set; }
    public required TaskCron Cron { get; init; }

    [Precision(3, 2)]
    [Range(0.00, 1.00, ErrorMessage = "The value must be between 0.00 and 1.00.")]
    public decimal Progress { get; set; }

    public Guid? ScheduledTaskConfigId { get; init; }
    public ScheduledTaskConfig? ScheduledTaskConfig { get; init; }
    public JsonDocument Settings { get; init; } = JsonDocument.Parse("{}");

    public required DateTimeOffset HaveToStartAt { get; init; }
}
