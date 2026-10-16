using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

// Registra cada abono (pago parcial o adelanto) que un cliente hace sobre
// una cuota, para tener trazabilidad completa. La cuota acumula el total
// pagado en MontoPagado.
[Table("abonos_cuota")]
public class AbonoCuota
{
    [Key]
    public long Id { get; set; }

    [Column("cuota_id")]
    public long CuotaId { get; set; }
    public Cuota? Cuota { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Monto { get; set; }

    [Column("fecha_abono"), DataType(DataType.Date)]
    public DateTime FechaAbono { get; set; }

    [Column("forma_pago")]
    public FormaPago? FormaPago { get; set; }

    public string? Observaciones { get; set; }

    [Column("usuario_registro_id")]
    public long UsuarioRegistroId { get; set; }
    public Usuario? UsuarioRegistro { get; set; }

    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; }
}
