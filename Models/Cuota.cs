using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardJym.Models;

[Table("cuotas")]
public class Cuota
{
    [Key]
    public long Id { get; set; }

    [Column("pago_id")]
    public long PagoId { get; set; }
    public Pago? Pago { get; set; }

    [Column("numero_cuota")]
    public int NumeroCuota { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Monto { get; set; }

    [Column("monto_pagado", TypeName = "decimal(10,2)")]
    public decimal MontoPagado { get; set; } = 0;

    [Column("fecha_vencimiento"), DataType(DataType.Date)]
    public DateTime? FechaVencimiento { get; set; }

    [Column("fecha_pago"), DataType(DataType.Date)]
    public DateTime? FechaPago { get; set; }

    public EstadoCuota Estado { get; set; } = EstadoCuota.PENDIENTE;

    [Column("forma_pago")]
    public FormaPago? FormaPago { get; set; }

    public string? Observaciones { get; set; }

    // Saldo que aun falta pagar de esta cuota. Igual que el @Transient de
    // Java: nunca es negativo.
    [NotMapped]
    public decimal SaldoPendiente => Math.Max(Monto - MontoPagado, 0);

    // Antes nada comparaba FechaVencimiento contra la fecha de hoy: una
    // cuota vencida se veia identica a una cuota al dia en toda la UI.
    // Antes de este calculo, DateTime.Today ya corre en hora de Peru (ver
    // TZ=America/Lima en el Dockerfile).
    [NotMapped]
    public bool EstaVencida =>
        (Estado == EstadoCuota.PENDIENTE || Estado == EstadoCuota.PARCIAL) &&
        FechaVencimiento.HasValue && FechaVencimiento.Value.Date < DateTime.Today;
}
