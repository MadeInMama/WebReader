namespace WebReader.Models.Entities;

public class BaseEntity
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset UpdatedDate { get; set; }
}