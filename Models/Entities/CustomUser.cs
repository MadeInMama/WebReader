using System.ComponentModel.DataAnnotations;

namespace WebReader.Models.Entities;

public class CustomUser : BaseEntity
{
    [MaxLength(256)] public string Username { get; init; } = string.Empty;
    [MaxLength(256)] public string PasswordHash { get; init; } = string.Empty;
    public IEnumerable<RoleType> Roles { get; init; } = [RoleType.User];
    public bool IsActive { get; init; } = true;
}