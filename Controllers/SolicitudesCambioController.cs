using System.Text.Json;
using DashboardJym.Data;
using DashboardJym.Models;
using DashboardJym.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Controllers;

// Bandeja de aprobaciones: exclusiva de los ABOGADOS. Aqui llegan las
// ediciones y desactivaciones propuestas por los asesores legales; hasta
// que un abogado apruebe, el registro original queda intacto.
[Authorize(Roles = PermisosUtil.RolAbogado)]
[Route("app/solicitudes")]
public class SolicitudesCambioController : Controller
{
    private readonly AppDbContext _context;
    private readonly SesionUtil _sesionUtil;
    private readonly RegistroActividadHelper _registroActividad;
    private readonly NotificacionHelper _notificacion;
    private readonly ILogger<SolicitudesCambioController> _logger;

    public SolicitudesCambioController(AppDbContext context, SesionUtil sesionUtil,
        RegistroActividadHelper registroActividad, NotificacionHelper notificacion,
        ILogger<SolicitudesCambioController> logger)
    {
        _context = context;
        _sesionUtil = sesionUtil;
        _registroActividad = registroActividad;
        _notificacion = notificacion;
        _logger = logger;
    }

    // GET /app/solicitudes
    [HttpGet]
    public async Task<IActionResult> Listar(string? estado)
    {
        var query = _context.SolicitudesCambio
            .Include(s => s.Solicitante)
            .Include(s => s.Revisor)
            .AsQueryable();

        if (Enum.TryParse<EstadoSolicitud>(estado, true, out var estadoFiltro))
        {
            query = query.Where(s => s.Estado == estadoFiltro);
        }
        else
        {
            query = query.Where(s => s.Estado == EstadoSolicitud.PENDIENTE);
        }

        var solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();

        ViewData["Title"] = "Solicitudes de cambio";
        ViewBag.EstadoActual = estado ?? "PENDIENTE";
        return View("Lista", solicitudes);
    }

    // POST /app/solicitudes/{id}/aprobar
    [HttpPost("{id:long}/aprobar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(long id)
    {
        var solicitud = await _context.SolicitudesCambio
            .Include(s => s.Solicitante)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        if (solicitud.Estado != EstadoSolicitud.PENDIENTE)
        {
            TempData["Mensaje"] = "Esa solicitud ya fue revisada.";
            return RedirectToAction(nameof(Listar));
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        try
        {
            await AplicarCambioAsync(solicitud);
        }
        catch (Exception ex)
        {
            // Antes se mostraba ex.Message directamente al usuario, lo que
            // puede filtrar detalles internos (nombres de tablas, columnas,
            // mensajes de PostgreSQL). Se registra el detalle real en el
            // log del servidor y al usuario se le da un mensaje genérico.
            _logger.LogError(ex, "Error al aplicar solicitud de cambio {Id}", id);
            TempData["Mensaje"] = "No se pudo aplicar el cambio. Intenta nuevamente o contacta al administrador.";
            return RedirectToAction(nameof(Listar));
        }

        solicitud.Estado = EstadoSolicitud.APROBADA;
        solicitud.RevisorId = usuarioActual.Id;
        solicitud.FechaRevision = DateTime.Now;
        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Aprobó solicitud de cambio",
            solicitud.Modulo, solicitud.RegistroId,
            $"Aprobó la solicitud de {solicitud.Solicitante?.NombreCompleto}: {solicitud.Descripcion}");

        await _notificacion.NotificarAsync(solicitud.SolicitanteId,
            "Solicitud aprobada",
            $"{usuarioActual.NombreCompleto} aprobó tu solicitud: {solicitud.Descripcion}",
            "SOLICITUD_CAMBIO", solicitud.Id);

        TempData["Mensaje"] = "Solicitud aprobada y cambio aplicado correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    // POST /app/solicitudes/{id}/rechazar
    [HttpPost("{id:long}/rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(long id, string? motivo)
    {
        var solicitud = await _context.SolicitudesCambio
            .Include(s => s.Solicitante)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        if (solicitud.Estado != EstadoSolicitud.PENDIENTE)
        {
            TempData["Mensaje"] = "Esa solicitud ya fue revisada.";
            return RedirectToAction(nameof(Listar));
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        solicitud.Estado = EstadoSolicitud.RECHAZADA;
        solicitud.RevisorId = usuarioActual.Id;
        solicitud.FechaRevision = DateTime.Now;
        solicitud.MotivoRechazo = motivo;
        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Rechazó solicitud de cambio",
            solicitud.Modulo, solicitud.RegistroId,
            $"Rechazó la solicitud de {solicitud.Solicitante?.NombreCompleto}: {solicitud.Descripcion}" +
                (!string.IsNullOrWhiteSpace(motivo) ? $" — Motivo: {motivo}" : ""));

        await _notificacion.NotificarAsync(solicitud.SolicitanteId,
            "Solicitud rechazada",
            $"{usuarioActual.NombreCompleto} rechazó tu solicitud: {solicitud.Descripcion}" +
                (!string.IsNullOrWhiteSpace(motivo) ? $" — Motivo: {motivo}" : ""),
            "SOLICITUD_CAMBIO", solicitud.Id);

        TempData["Mensaje"] = "Solicitud rechazada.";
        return RedirectToAction(nameof(Listar));
    }

    // Vuelca los valores propuestos sobre el registro real. Solo se llama
    // cuando un abogado aprueba: es el unico punto donde una edicion de
    // asesor llega efectivamente a la base de datos.
    private async Task AplicarCambioAsync(SolicitudCambio solicitud)
    {
        if (solicitud.Modulo == "Clientes")
        {
            var cliente = await _context.Clientes.FindAsync(solicitud.RegistroId)
                ?? throw new InvalidOperationException("El cliente ya no existe.");

            if (solicitud.TipoAccion == TipoAccionSolicitud.DESACTIVAR)
            {
                cliente.Estado = false;
            }
            else
            {
                var datos = JsonSerializer.Deserialize<Cliente>(solicitud.DatosJson!)
                    ?? throw new InvalidOperationException("Los datos propuestos no son válidos.");

                cliente.Nombres = datos.Nombres;
                cliente.Apellidos = datos.Apellidos;
                cliente.Dni = datos.Dni;
                cliente.FechaEmisionDni = datos.FechaEmisionDni;
                cliente.FechaNacimiento = datos.FechaNacimiento;
                cliente.NombrePadre = datos.NombrePadre;
                cliente.NombreMadre = datos.NombreMadre;
                cliente.Whatsapp = datos.Whatsapp;
                cliente.Correo = datos.Correo;
                cliente.Observaciones = datos.Observaciones;
            }

            cliente.FechaActualizacion = DateTime.Now;
        }
        else if (solicitud.Modulo == "Procesos")
        {
            var proceso = await _context.Procesos.FindAsync(solicitud.RegistroId)
                ?? throw new InvalidOperationException("El proceso ya no existe.");

            var datos = JsonSerializer.Deserialize<Proceso>(solicitud.DatosJson!)
                ?? throw new InvalidOperationException("Los datos propuestos no son válidos.");

            proceso.ClienteId = datos.ClienteId;
            proceso.TipoProcesoId = datos.TipoProcesoId;
            proceso.Materia = datos.Materia;
            proceso.Descripcion = datos.Descripcion;
            proceso.UltimoActuado = datos.UltimoActuado;
            proceso.NumeroExpediente = datos.NumeroExpediente;
            proceso.NumeroCarpeta = datos.NumeroCarpeta;
            proceso.JuzgadoFiscalia = datos.JuzgadoFiscalia;
            proceso.EspecialistaLegal = datos.EspecialistaLegal;
            proceso.DistritoJudicial = datos.DistritoJudicial;
            proceso.FechaInicio = datos.FechaInicio;
            proceso.Estado = datos.Estado;
            proceso.Prioridad = datos.Prioridad;
            proceso.AbogadoResponsableId = datos.AbogadoResponsableId;
            proceso.AsesorResponsableId = datos.AsesorResponsableId;
            proceso.Observaciones = datos.Observaciones;
            proceso.FechaActualizacion = DateTime.Now;
        }
        else
        {
            throw new InvalidOperationException($"Módulo no soportado: {solicitud.Modulo}");
        }

        await _context.SaveChangesAsync();
    }
}
