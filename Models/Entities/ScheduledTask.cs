using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace WebReader.Models.Entities;

public class ScheduledTask : BaseEntity
{
    public TaskType Type { get; init; }
    public sbyte Priority { get; init; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public string? ErrorMessage { get; set; }

    [Precision(3, 2)]
    [Range(0.00, 1.00, ErrorMessage = "The value must be between 0.00 and 1.00.")]
    public decimal Progress { get; set; }

    //TODO: Progress

    public Guid? ScheduledTaskConfigId { get; init; }
    public ScheduledTaskConfig? ScheduledTaskConfig { get; init; }
}
