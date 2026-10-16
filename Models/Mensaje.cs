using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("mensajes")]
public class Mensaje
{
    [Key]
    public long Id { get; set; }

    [Column("remitente_id")]
    public long RemitenteId { get; set; }
    public Usuario? Remitente { get; set; }

    [Column("destinatario_id")]
    public long DestinatarioId { get; set; }
    public Usuario? Destinatario { get; set; }

    [MaxLength(200)]
    public string? Asunto { get; set; }

    [Required]
    public string Contenido { get; set; } = string.Empty;

    public bool Leido { get; set; } = false;

    [Column("fecha_lectura")]
    public DateTime? FechaLectura { get; set; }

    [Column("fecha_envio")]
    public DateTime FechaEnvio { get; set; }
}
