using System.ComponentModel.DataAnnotations;

namespace WebReader.Models.Dtos.Rest;

public class LoginRequestDto
{
    [Required] public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
