using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("agenda")]
public class ActividadAgenda
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Column("cliente_id")]
    public long? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    [Column("proceso_id")]
    public long? ProcesoId { get; set; }
    public Proceso? Proceso { get; set; }

    public TipoAgenda Tipo { get; set; }

    [DataType(DataType.Date)]
    public DateTime Fecha { get; set; }

    [DataType(DataType.Time)]
    public TimeSpan Hora { get; set; }

    public int? Duracion { get; set; }

    [MaxLength(255)]
    public string? Lugar { get; set; }

    [Column("responsable_id")]
    public long ResponsableId { get; set; }
    public Usuario? Responsable { get; set; }

    public EstadoAgenda Estado { get; set; } = EstadoAgenda.PENDIENTE;

    public string? Observaciones { get; set; }

    [Column("usuario_registro_id")]
    public long UsuarioRegistroId { get; set; }
    public Usuario? UsuarioRegistro { get; set; }
}
