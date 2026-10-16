using System.Text.Json;
using DashboardJym.Data;
using DashboardJym.Models;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Util;

// Crea las propuestas de cambio que envian los asesores legales y avisa
// a los abogados para que las revisen.
public class SolicitudCambioHelper
{
    private readonly AppDbContext _context;
    private readonly NotificacionHelper _notificacion;
    private readonly RegistroActividadHelper _registroActividad;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    };

    public SolicitudCambioHelper(AppDbContext context, NotificacionHelper notificacion,
        RegistroActividadHelper registroActividad)
    {
        _context = context;
        _notificacion = notificacion;
        _registroActividad = registroActividad;
    }

    public async Task<SolicitudCambio> CrearAsync<T>(string modulo, long registroId,
        TipoAccionSolicitud tipoAccion, T? datosPropuestos, string descripcion,
        string? resumenCambios, long solicitanteId)
    {
        var solicitud = new SolicitudCambio
        {
            Modulo = modulo,
            RegistroId = registroId,
            TipoAccion = tipoAccion,
            DatosJson = datosPropuestos == null ? null : JsonSerializer.Serialize(datosPropuestos, OpcionesJson),
            ResumenCambios = resumenCambios,
            Descripcion = descripcion,
            SolicitanteId = solicitanteId,
            Estado = EstadoSolicitud.PENDIENTE,
            FechaSolicitud = DateTime.Now,
        };

        _context.SolicitudesCambio.Add(solicitud);
        await _context.SaveChangesAsync();

        var solicitante = await _context.Usuarios.FindAsync(solicitanteId);
        var nombreSolicitante = solicitante?.NombreCompleto ?? "Un asesor legal";

        // Solo los abogados activos reciben el aviso de revision.
        var abogados = await _context.Usuarios
            .Where(u => u.Estado && u.UsuarioRoles.Any(ur => ur.Rol.Nombre == PermisosUtil.RolAbogado))
            .ToListAsync();

        foreach (var abogado in abogados)
        {
            await _notificacion.NotificarAsync(abogado.Id,
                "Solicitud de cambio pendiente",
                $"{nombreSolicitante} solicitó: {descripcion}. Requiere tu aprobación.",
                "SOLICITUD_CAMBIO", solicitud.Id);
        }

        await _registroActividad.RegistrarAsync(solicitanteId, "Solicitó cambio", modulo, registroId,
            $"Solicitó aprobación para: {descripcion}");

        return solicitud;
    }

    // Compara dos objetos campo por campo y arma un resumen legible para
    // que el abogado sepa exactamente que se esta cambiando.
    public static string ResumirCambios<T>(T original, T propuesto, params string[] propiedades)
    {
        var lineas = new List<string>();
        var tipo = typeof(T);

        foreach (var nombre in propiedades)
        {
            var prop = tipo.GetProperty(nombre);
            if (prop == null) continue;

            var antes = prop.GetValue(original)?.ToString() ?? "(vacío)";
            var despues = prop.GetValue(propuesto)?.ToString() ?? "(vacío)";

            if (antes != despues)
            {
                lineas.Add($"{nombre}: \"{antes}\" → \"{despues}\"");
            }
        }

        return lineas.Count == 0 ? "Sin cambios detectados." : string.Join("\n", lineas);
    }
}
