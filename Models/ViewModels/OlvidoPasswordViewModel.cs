using System.ComponentModel.DataAnnotations;

namespace DashboardJym.Models.ViewModels;

public class OlvidoPasswordViewModel
{
    [Required, EmailAddress]
    public string Correo { get; set; } = string.Empty;
}
