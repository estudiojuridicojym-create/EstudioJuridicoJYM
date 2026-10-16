using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("usuarios")]
public class Usuario
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(100)]
    public string Nombres { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Apellidos { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Dni { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Correo { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Telefono { get; set; }

    [Required, MaxLength(255)]
    public string Password { get; set; } = string.Empty;

    public bool Estado { get; set; } = true;

    [Column("fecha_creacion")]
    public DateTime? FechaCreacion { get; set; }

    [Column("fecha_actualizacion")]
    public DateTime? FechaActualizacion { get; set; }

    // Navegacion (equivalente a la relacion via usuarios_roles)
    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();

    [NotMapped]
    public string NombreCompleto => $"{Nombres} {Apellidos}";
}
