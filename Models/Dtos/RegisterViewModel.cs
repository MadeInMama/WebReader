using System.ComponentModel.DataAnnotations;

namespace WebReader.Models.Dtos;

public class RegisterViewModel
{
    [Required] public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(256, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;

    // [DataType(DataType.Password)]
    // [Compare("Password")]
    // public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public bool RememberMe { get; set; } = false;
}