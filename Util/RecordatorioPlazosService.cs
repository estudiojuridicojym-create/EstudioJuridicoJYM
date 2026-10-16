using DashboardJym.Data;
using DashboardJym.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DashboardJym.Util;

// Antes no existía ningún aviso automático de plazos ni audiencias: si
// nadie revisaba la agenda ese día, un vencimiento procesal o una
// audiencia podían pasar completamente desapercibidos. Este servicio
// corre en segundo plano durante toda la vida de la aplicación y, una vez
// al día, le crea una notificación en el sistema al responsable (y a quien
// registró la actividad, si es otra persona) de cada PLAZO o AUDIENCIA que
// vence mañana.
//
// AppDbContext y NotificacionHelper son "Scoped" (uno por petición HTTP),
// pero este servicio es "Singleton" (vive todo el proceso), así que no se
// le puede inyectar el DbContext directamente: hay que pedirle a
// IServiceScopeFactory un scope nuevo en cada vuelta del ciclo.
public class RecordatorioPlazosService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordatorioPlazosService> _logger;

    // Cada cuánto se revisa si ya toca mandar el recordatorio del día.
    // No hace falta que sea exactamente a medianoche: una revisión por
    // hora es más que suficiente y barata.
    private static readonly TimeSpan IntervaloRevision = TimeSpan.FromHours(1);

    public RecordatorioPlazosService(IServiceScopeFactory scopeFactory, ILogger<RecordatorioPlazosService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(IntervaloRevision);

        // Corre una vez apenas arranca la app y luego en cada tick del timer.
        do
        {
            try
            {
                await GenerarRecordatoriosDelDiaAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando recordatorios de plazos/audiencias");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await temporizador.WaitForNextTickAsync(stoppingToken));
    }

    private async Task GenerarRecordatoriosDelDiaAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificaciones = scope.ServiceProvider.GetRequiredService<NotificacionHelper>();

        var hoy = DateTime.Today;
        var manana = hoy.AddDays(1);

        var actividadesManana = await context.Agenda
            .Where(a => (a.Tipo == TipoAgenda.PLAZO || a.Tipo == TipoAgenda.AUDIENCIA)
                && a.Fecha == manana
                && a.Estado != EstadoAgenda.CANCELADA
                && a.Estado != EstadoAgenda.REALIZADA)
            .ToListAsync(ct);

        foreach (var actividad in actividadesManana)
        {
            // Evita mandar el mismo recordatorio varias veces el mismo día
            // si el servicio se reinicia o hay más de un tick antes de que
            // cambie la fecha.
            var yaAvisadoHoy = await context.Notificaciones.AnyAsync(n =>
                n.Tipo == "RECORDATORIO_AGENDA" && n.ReferenciaId == actividad.Id
                && n.FechaCreacion.Date == hoy, ct);

            if (yaAvisadoHoy) continue;

            var etiquetaTipo = actividad.Tipo == TipoAgenda.PLAZO ? "Plazo" : "Audiencia";
            var titulo = $"{etiquetaTipo} mañana: {actividad.Titulo}";
            var mensaje = $"\"{actividad.Titulo}\" vence/ocurre mañana ({actividad.Fecha:dd/MM/yyyy})" +
                (string.IsNullOrWhiteSpace(actividad.Lugar) ? "." : $" en {actividad.Lugar}.");

            await notificaciones.NotificarAsync(actividad.ResponsableId, titulo, mensaje, "RECORDATORIO_AGENDA", actividad.Id);

            // Si quien registró la actividad no es el responsable, también
            // se le avisa (ej. un asesor registró el plazo a nombre de un
            // abogado).
            if (actividad.UsuarioRegistroId != actividad.ResponsableId)
            {
                await notificaciones.NotificarAsync(actividad.UsuarioRegistroId, titulo, mensaje, "RECORDATORIO_AGENDA", actividad.Id);
            }
        }
    }
}
