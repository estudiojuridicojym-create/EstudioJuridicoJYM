using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("pagos")]
public class Pago
{
    [Key]
    public long Id { get; set; }

    [Column("cliente_id")]
    public long ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    [Column("proceso_id")]
    public long? ProcesoId { get; set; }
    public Proceso? Proceso { get; set; }

    [Required, MaxLength(255)]
    public string Concepto { get; set; } = string.Empty;

    [Column("monto_total", TypeName = "decimal(10,2)")]
    public decimal MontoTotal { get; set; }

    [Column("forma_pago")]
    public FormaPago FormaPago { get; set; }

    // HONORARIOS: se divide automaticamente en 3 cuotas (50/25/25).
    // EXTRA: pago externo/aparte (tasas, edictos, gastos), un solo pago sin dividir.
    [Column("tipo_pago")]
    public TipoPago TipoPago { get; set; } = TipoPago.HONORARIOS;

    [Column("numero_cuotas")]
    public int NumeroCuotas { get; set; } = 1;

    public EstadoPago Estado { get; set; } = EstadoPago.PENDIENTE;

    [Column("fecha_pago"), DataType(DataType.Date)]
    public DateTime FechaPago { get; set; }

    public string? Observaciones { get; set; }

    [Column("usuario_registro_id")]
    public long UsuarioRegistroId { get; set; }
    public Usuario? UsuarioRegistro { get; set; }

    [Column("usuario_confirmacion_id")]
    public long? UsuarioConfirmacionId { get; set; }
    public Usuario? UsuarioConfirmacion { get; set; }

    [Column("fecha_confirmacion")]
    public DateTime? FechaConfirmacion { get; set; }

    [Column("fecha_registro")]
    public DateTime? FechaRegistro { get; set; }

    [Column("fecha_actualizacion")]
    public DateTime? FechaActualizacion { get; set; }

    public ICollection<Cuota> Cuotas { get; set; } = new List<Cuota>();
}
