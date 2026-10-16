namespace DashboardJym.Models;

public enum TipoAgenda
{
    CITA,
    AUDIENCIA,
    REUNION,
    CONSULTA,
    // Vencimiento procesal (apelación, contestación, subsanación, etc.).
    // Es, junto con AUDIENCIA, lo más riesgoso de perder en un estudio
    // jurídico; por eso RecordatorioPlazosService avisa un día antes.
    PLAZO
}

public enum EstadoAgenda
{
    PENDIENTE,
    CONFIRMADA,
    REALIZADA,
    CANCELADA
}
