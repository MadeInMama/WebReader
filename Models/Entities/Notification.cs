using System.Text.Json;

namespace WebReader.Models.Entities;

public class Notification : BaseEntity
{
    public Guid? UserId { get; init; }
    public CustomUser? User { get; set; }
    public bool IsSent { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public required NotificationType Type { get; init; }
    public required JsonDocument Message { get; init; } = JsonDocument.Parse("{}");

    public bool IsPersonal()
    {
        return UserId != null;
    }

    public void SetSent()
    {
        IsSent = true;
        SentAt = DateTimeOffset.UtcNow;
    }
}
