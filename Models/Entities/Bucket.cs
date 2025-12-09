using System.ComponentModel.DataAnnotations;

namespace WebReader.Models.Entities;

public class Bucket : BaseEntity
{
    [MaxLength(256)] public required string Name { get; init; }

    [MaxLength(256)] public string? CustomName { get; init; }

    //TODO: roles helper (GetAllRoles, CompareRoles, ...)
    public IEnumerable<RoleType> AccessRoles { get; init; } = [RoleType.Admin, RoleType.User];
    public bool IsHidden { get; set; }
    public bool IsAvailable { get; set; }

    public Guid? UserId { get; init; }
    public CustomUser? User { get; init; }
}
