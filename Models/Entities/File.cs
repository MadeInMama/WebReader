using System.ComponentModel.DataAnnotations;

namespace WebReader.Models.Entities;

public class File : BaseEntity
{
    [MaxLength(256)] public required string Bucket { get; init; }
    [MaxLength(256)] public required string Object { get; init; }
    [MaxLength(256)] public string? CustomName { get; init; }
    public FileType Type { get; init; }
    public IEnumerable<RoleType> AccessRoles { get; init; } = [RoleType.Admin, RoleType.User];
    public bool IsHidden { get; init; }
}