namespace DashboardJym.Models;

public enum FormaPago
{
    YAPE,
    PLIN,
    TRANSFERENCIA,
    EFECTIVO
}

public enum EstadoPago
{
    PENDIENTE,
    CONFIRMADO,
    OBSERVADO,
    ANULADO
}

public enum EstadoCuota
{
    PENDIENTE,
    PARCIAL,
    PAGADA,
    OBSERVADA,
    ANULADA
}

public enum TipoPago
{
    HONORARIOS,
    EXTRA
}
