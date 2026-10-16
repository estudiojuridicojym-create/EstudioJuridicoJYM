using System.ComponentModel.DataAnnotations;

namespace DashboardJym.Models.ViewModels;

public class RestablecerPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; set; } = string.Empty;
}
