namespace WebReader.Models.Entities;

public class SubscriberTg : BaseEntity
{
    public required long ChatId { get; set; }
    public bool IsActive { get; init; } = true;
    public Guid? UserId { get; set; }
    public CustomUser? User { get; set; }
}
