using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("clientes")]
public class Cliente
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(100)]
    public string Nombres { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Apellidos { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Dni { get; set; }

    [Column("fecha_emision_dni"), DataType(DataType.Date)]
    public DateTime? FechaEmisionDni { get; set; }

    [DataType(DataType.Date)]
    public DateTime? FechaNacimiento { get; set; }

    [Column("nombre_padre"), MaxLength(100)]
    public string? NombrePadre { get; set; }

    [Column("nombre_madre"), MaxLength(100)]
    public string? NombreMadre { get; set; }

    [MaxLength(20)]
    public string? Whatsapp { get; set; }

    [MaxLength(150)]
    public string? Correo { get; set; }

    public string? Observaciones { get; set; }

    [Column("usuario_registro_id")]
    public long UsuarioRegistroId { get; set; }

    public Usuario? UsuarioRegistro { get; set; }

    public bool Estado { get; set; } = true;

    [Column("fecha_registro")]
    public DateTime? FechaRegistro { get; set; }

    [Column("fecha_actualizacion")]
    public DateTime? FechaActualizacion { get; set; }
}
