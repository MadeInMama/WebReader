using System.ComponentModel.DataAnnotations;

namespace WebReader.Models.Entities;

public class Settings : BaseEntity
{
    [MaxLength(256)] public required string Key { get; set; }

    [MaxLength(1024)] public required string Value { get; set; }
}
