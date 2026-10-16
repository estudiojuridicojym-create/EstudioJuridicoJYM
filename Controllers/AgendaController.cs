using System.Globalization;
using DashboardJym.Data;
using DashboardJym.Models;
using DashboardJym.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Controllers;

[Authorize]
[Route("app/agenda")]
public class AgendaController : Controller
{
    private readonly AppDbContext _context;
    private readonly SesionUtil _sesionUtil;
    private readonly NotificacionHelper _notificaciones;
    private readonly RegistroActividadHelper _registroActividad;

    public AgendaController(AppDbContext context, SesionUtil sesionUtil, NotificacionHelper notificaciones, RegistroActividadHelper registroActividad)
    {
        _context = context;
        _sesionUtil = sesionUtil;
        _notificaciones = notificaciones;
        _registroActividad = registroActividad;
    }

    // GET /app/agenda — proximas actividades (desde hoy en adelante)
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var hoy = DateTime.Today;
        var actividades = await _context.Agenda
            .Include(a => a.Cliente)
            .Include(a => a.Responsable)
            .Where(a => a.Fecha >= hoy)
            .OrderBy(a => a.Fecha).ThenBy(a => a.Hora)
            .ToListAsync();

        ViewData["Title"] = "Agenda";
        return View("Lista", actividades);
    }

    // GET /app/agenda/calendario?anio=...&mes=...
    [HttpGet("calendario")]
    public async Task<IActionResult> Calendario(int? anio, int? mes)
    {
        var mesActual = (anio.HasValue && mes.HasValue)
            ? new DateTime(anio.Value, mes.Value, 1)
            : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var primerDia = mesActual;
        var ultimoDia = mesActual.AddMonths(1).AddDays(-1);

        var actividadesDelMes = await _context.Agenda
            .Where(a => a.Fecha >= primerDia && a.Fecha <= ultimoDia)
            .OrderBy(a => a.Fecha).ThenBy(a => a.Hora)
            .ToListAsync();

        var porDia = actividadesDelMes
            .GroupBy(a => a.Fecha.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewBag.Semanas = ConstruirSemanas(mesActual);
        ViewBag.PorDia = porDia;
        ViewBag.MesActual = mesActual;

        var cultura = new CultureInfo("es-PE");
        ViewBag.NombreMes = cultura.DateTimeFormat.GetMonthName(mesActual.Month);
        ViewBag.MesAnterior = mesActual.AddMonths(-1);
        ViewBag.MesSiguiente = mesActual.AddMonths(1);

        ViewData["Title"] = "Calendario";
        return View("Calendario");
    }

    // Arma la grilla de semanas (lunes a domingo) del mes para la vista calendario.
    private static List<List<DateTime>> ConstruirSemanas(DateTime mes)
    {
        var primerDiaMes = new DateTime(mes.Year, mes.Month, 1);
        var ultimoDiaMes = primerDiaMes.AddMonths(1).AddDays(-1);

        // DayOfWeek: Sunday=0..Saturday=6; queremos que la semana empiece en lunes.
        int diaSemanaISO(DateTime d) => ((int)d.DayOfWeek == 0) ? 7 : (int)d.DayOfWeek;

        var inicioGrilla = primerDiaMes.AddDays(-(diaSemanaISO(primerDiaMes) - 1));
        var finGrilla = ultimoDiaMes.AddDays(7 - diaSemanaISO(ultimoDiaMes));

        var semanas = new List<List<DateTime>>();
        var cursor = inicioGrilla;
        while (cursor <= finGrilla)
        {
            var semana = new List<DateTime>();
            for (int i = 0; i < 7; i++)
            {
                semana.Add(cursor);
                cursor = cursor.AddDays(1);
            }
            semanas.Add(semana);
        }
        return semanas;
    }

    // GET /app/agenda/nuevo?clienteId=...
    [HttpGet("nuevo")]
    public async Task<IActionResult> FormularioNuevo(long? clienteId)
    {
        var actividad = new ActividadAgenda { Fecha = DateTime.Today };
        if (clienteId.HasValue) actividad.ClienteId = clienteId.Value;

        ViewData["Title"] = "Nueva actividad";
        ViewData["EsNuevo"] = true;
        await CargarListasApoyoAsync();
        return View("Formulario", actividad);
    }

    // POST /app/agenda
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(ActividadAgenda actividad)
    {
        LimpiarModelStateNavegacion();

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Nueva actividad";
            ViewData["EsNuevo"] = true;
            await CargarListasApoyoAsync();
            return View("Formulario", actividad);
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        actividad.UsuarioRegistroId = usuarioActual.Id;
        actividad.Estado = EstadoAgenda.PENDIENTE;

        _context.Agenda.Add(actividad);
        await _context.SaveChangesAsync();

        var tipoTexto = actividad.Tipo.ToString().ToLower();
        await _registroActividad.RegistrarAsync(usuarioActual.Id, $"Creó {tipoTexto}", "Agenda", actividad.Id,
            $"Creó \"{actividad.Titulo}\" para el {actividad.Fecha:dd/MM/yyyy} {actividad.Hora:hh\\:mm}");

        await AvisarSiResponsableEsOtroAsync(actividad, usuarioActual.Id,
            "Nueva actividad en tu agenda",
            $"{usuarioActual.Nombres} te asignó \"{actividad.Titulo}\" el {actividad.Fecha:dd/MM/yyyy} a las {actividad.Hora:hh\\:mm}");

        TempData["Mensaje"] = "Actividad registrada correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    // GET /app/agenda/{id}/editar
    [HttpGet("{id:long}/editar")]
    public async Task<IActionResult> FormularioEditar(long id)
    {
        var actividad = await _context.Agenda.FindAsync(id);
        if (actividad == null) return NotFound();

        ViewData["Title"] = "Editar actividad";
        ViewData["EsNuevo"] = false;
        await CargarListasApoyoAsync();
        return View("Formulario", actividad);
    }

    // POST /app/agenda/{id}
    [HttpPost("{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(long id, ActividadAgenda datos)
    {
        LimpiarModelStateNavegacion();

        var actividad = await _context.Agenda.FindAsync(id);
        if (actividad == null) return NotFound();

        // Las audiencias son el evento más sensible de la agenda (perderlas
        // tiene consecuencias procesales); solo un abogado puede reprogramarlas
        // o modificarlas. Un asesor puede seguir editando citas, reuniones y
        // consultas libremente.
        if (actividad.Tipo == TipoAgenda.AUDIENCIA && !PermisosUtil.EsAbogado(User))
        {
            TempData["Mensaje"] = "Solo un abogado puede editar una audiencia. Pídele a un abogado que haga este cambio.";
            return RedirectToAction(nameof(Listar));
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar actividad";
            ViewData["EsNuevo"] = false;
            datos.Id = id;
            await CargarListasApoyoAsync();
            return View("Formulario", datos);
        }

        actividad.Titulo = datos.Titulo;
        actividad.ClienteId = datos.ClienteId;
        actividad.ProcesoId = datos.ProcesoId;
        actividad.Tipo = datos.Tipo;
        actividad.Fecha = datos.Fecha;
        actividad.Hora = datos.Hora;
        actividad.Duracion = datos.Duracion;
        actividad.Lugar = datos.Lugar;
        actividad.ResponsableId = datos.ResponsableId;
        actividad.Observaciones = datos.Observaciones;

        await _context.SaveChangesAsync();

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        var tipoTexto = actividad.Tipo.ToString().ToLower();
        await _registroActividad.RegistrarAsync(usuarioActual.Id, $"Modificó {tipoTexto}", "Agenda", actividad.Id,
            $"Modificó \"{actividad.Titulo}\"");

        await AvisarSiResponsableEsOtroAsync(actividad, usuarioActual.Id,
            "Actividad modificada",
            $"{usuarioActual.Nombres} modificó \"{actividad.Titulo}\" ({actividad.Fecha:dd/MM/yyyy} {actividad.Hora:hh\\:mm})");

        TempData["Mensaje"] = "Actividad actualizada correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    // POST /app/agenda/{id}/estado
    [HttpPost("{id:long}/estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(long id, EstadoAgenda estado)
    {
        var actividad = await _context.Agenda.FindAsync(id);
        if (actividad == null) return NotFound();

        if (actividad.Tipo == TipoAgenda.AUDIENCIA && !PermisosUtil.EsAbogado(User))
        {
            TempData["Mensaje"] = "Solo un abogado puede confirmar o cancelar una audiencia.";
            return RedirectToAction(nameof(Listar));
        }

        actividad.Estado = estado;
        await _context.SaveChangesAsync();

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        var accion = estado switch
        {
            EstadoAgenda.CONFIRMADA => "Confirmó",
            EstadoAgenda.REALIZADA => "Marcó realizada",
            EstadoAgenda.CANCELADA => "Canceló",
            _ => "Actualizó",
        };
        await AvisarSiResponsableEsOtroAsync(actividad, usuarioActual.Id,
            $"Actividad {estado.ToString().ToLower()}",
            $"{accion} \"{actividad.Titulo}\"");

        await _registroActividad.RegistrarAsync(usuarioActual.Id, $"{accion} actividad", "Agenda", actividad.Id,
            $"{accion} \"{actividad.Titulo}\"");

        TempData["Mensaje"] = "Actividad actualizada correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    private async Task AvisarSiResponsableEsOtroAsync(ActividadAgenda actividad, long usuarioQueActuaId, string titulo, string mensaje)
    {
        if (actividad.ResponsableId != usuarioQueActuaId)
        {
            await _notificaciones.NotificarAsync(actividad.ResponsableId, titulo, mensaje, "AGENDA", actividad.Id);
        }
    }

    private void LimpiarModelStateNavegacion()
    {
        ModelState.Remove(nameof(ActividadAgenda.Cliente));
        ModelState.Remove(nameof(ActividadAgenda.Proceso));
        ModelState.Remove(nameof(ActividadAgenda.Responsable));
        ModelState.Remove(nameof(ActividadAgenda.UsuarioRegistro));
    }

    private async Task CargarListasApoyoAsync()
    {
        ViewBag.Clientes = await _context.Clientes.Where(c => c.Estado).OrderBy(c => c.Apellidos).ToListAsync();
        ViewBag.Procesos = await _context.Procesos.ToListAsync();
        ViewBag.Usuarios = await _context.Usuarios.Where(u => u.Estado).OrderBy(u => u.Nombres).ToListAsync();
        ViewBag.Tipos = Enum.GetValues<TipoAgenda>();
    }
}
