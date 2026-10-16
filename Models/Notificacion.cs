using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("notificaciones")]
public class Notificacion
{
    [Key]
    public long Id { get; set; }

    [Column("usuario_id")]
    public long UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    [Required, MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Required]
    public string Mensaje { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Tipo { get; set; }

    [Column("referencia_id")]
    public long? ReferenciaId { get; set; }

    public bool Leida { get; set; } = false;

    [Column("fecha_lectura")]
    public DateTime? FechaLectura { get; set; }

    [Column("fecha_creacion")]
    public DateTime FechaCreacion { get; set; }
}
