namespace WebReader.Models.Entities;

public class UserTgLink : BaseEntity
{
    public Guid Code { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public CustomUser? User { get; set; }

    public bool IsUsed { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
