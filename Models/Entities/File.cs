using System.ComponentModel.DataAnnotations;

namespace WebReader.Models.Entities;

public class File : BaseEntity
{
    public required Guid BucketId { get; init; }
    public Bucket? Bucket { get; init; }
    [MaxLength(256)] public required string Name { get; init; }
    [MaxLength(256)] public string? CustomName { get; init; }
    public required FileType Type { get; set; }
    public ulong? Size { get; set; }
    public IEnumerable<RoleType> AccessRoles { get; init; } = [Enum.GetValues<RoleType>().Min()];
    public bool IsHidden { get; set; }
    public bool IsAvailable { get; set; }
}
