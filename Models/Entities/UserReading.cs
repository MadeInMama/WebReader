namespace WebReader.Models.Entities;

public class UserReading : BaseEntity
{
    public required Guid UserId { get; init; }
    public CustomUser? User { get; init; }
    public required Guid FileId { get; init; }
    public File? File { get; init; }
    public int Page { get; init; } = 1;
    public int Scale { get; init; } = 1;
}
