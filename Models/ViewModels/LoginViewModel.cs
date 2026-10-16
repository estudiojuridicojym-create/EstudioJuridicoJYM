using System.ComponentModel.DataAnnotations;

namespace DashboardJym.Models.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Correo { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
