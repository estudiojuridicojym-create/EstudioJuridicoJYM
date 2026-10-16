using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("procesos")]
public class Proceso
{
    [Key]
    public long Id { get; set; }

    [Column("cliente_id")]
    public long ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    [Column("tipo_proceso_id")]
    public long TipoProcesoId { get; set; }
    public TipoProceso? TipoProceso { get; set; }

    [Required, MaxLength(150)]
    public string Materia { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    [Column("numero_expediente"), MaxLength(100)]
    public string? NumeroExpediente { get; set; }

    [Column("numero_carpeta"), MaxLength(100)]
    public string? NumeroCarpeta { get; set; }

    [Column("ultimo_actuado")]
    public string? UltimoActuado { get; set; }

    [Column("juzgado_fiscalia"), MaxLength(200)]
    public string? JuzgadoFiscalia { get; set; }

    [Column("especialista_legal"), MaxLength(200)]
    public string? EspecialistaLegal { get; set; }

    [Column("distrito_judicial"), MaxLength(100)]
    public string? DistritoJudicial { get; set; }

    [Column("fecha_inicio"), DataType(DataType.Date)]
    public DateTime? FechaInicio { get; set; }

    public EstadoProceso Estado { get; set; } = EstadoProceso.NUEVO;

    public PrioridadProceso Prioridad { get; set; } = PrioridadProceso.MEDIA;

    [Column("abogado_responsable_id")]
    public long? AbogadoResponsableId { get; set; }
    public Usuario? AbogadoResponsable { get; set; }

    [Column("asesor_responsable_id")]
    public long? AsesorResponsableId { get; set; }
    public Usuario? AsesorResponsable { get; set; }

    public string? Observaciones { get; set; }

    [Column("usuario_registro_id")]
    public long UsuarioRegistroId { get; set; }
    public Usuario? UsuarioRegistro { get; set; }

    [Column("fecha_registro")]
    public DateTime? FechaRegistro { get; set; }

    [Column("fecha_actualizacion")]
    public DateTime? FechaActualizacion { get; set; }
}
