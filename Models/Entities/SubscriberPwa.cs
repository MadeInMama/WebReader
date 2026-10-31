namespace WebReader.Models.Entities;

public class SubscriberPwa : BaseEntity
{
    public required string Endpoint { get; set; }
    public required Dictionary<string, string> Keys { get; set; } = new();
    public bool IsActive { get; init; } = true;
    public required Guid UserId { get; set; }
    public CustomUser? User { get; set; }
}
