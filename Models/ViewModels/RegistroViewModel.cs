using System.ComponentModel.DataAnnotations;

namespace DashboardJym.Models.ViewModels;

public class RegistroViewModel
{
    [Required, MaxLength(100)]
    public string Nombres { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Apellidos { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Dni { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Telefono { get; set; }

    [Required, EmailAddress, MaxLength(150)]
    public string Correo { get; set; } = string.Empty;

    [Required, MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Password { get; set; } = string.Empty;

    // Código compartido solo con el equipo del estudio. El rol asignado NO
    // lo elige la persona que se registra: lo determina el propio código
    // (hay uno para abogados y otro para asesores legales), para que nadie
    // pueda auto-asignarse permisos de abogado con el código equivocado.
    [Required]
    public string CodigoInvitacion { get; set; } = string.Empty;
}
