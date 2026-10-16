using DashboardJym.Data;
using DashboardJym.Models;
using DashboardJym.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Controllers;

[Authorize]
public class HomeController : Controller
{
    private const int LimitePanel = 5;

    private readonly AppDbContext _context;
    private readonly SesionUtil _sesionUtil;

    public HomeController(AppDbContext context, SesionUtil sesionUtil)
    {
        _context = context;
        _sesionUtil = sesionUtil;
    }

    // GET / o /inicio - panel de inicio: indicadores del estudio (audiencias
    // de hoy, plazos y cuotas vencidas, por cobrar, ingresos del mes,
    // procesos activos, solicitudes pendientes) más los paneles de
    // actividad reciente (pagos pendientes, citas, audiencias, mensajes,
    // notificaciones). Antes el inicio era solo las listas, sin ningún
    // indicador ni alerta — lo que hacía que perder un plazo o acumular
    // cuotas vencidas pasara totalmente desapercibido.
    [Route("/")]
    [Route("/inicio")]
    public async Task<IActionResult> Index()
    {
        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        var esAbogado = PermisosUtil.EsAbogado(User);
        var hoy = DateTime.Today;
        var finSemana = hoy.AddDays(7);

        // --- Indicadores ---
        // Trae solo lo que necesita cada indicador, en vez de cargar toda
        // la agenda futura y filtrarla en memoria como hacía antes.
        ViewBag.AudienciasHoy = await _context.Agenda
            .CountAsync(a => a.Tipo == TipoAgenda.AUDIENCIA && a.Fecha == hoy && a.Estado != EstadoAgenda.CANCELADA);

        ViewBag.EventosSemana = await _context.Agenda
            .CountAsync(a => a.Fecha >= hoy && a.Fecha <= finSemana && a.Estado != EstadoAgenda.CANCELADA);

        var cuotasVencidas = await _context.Cuotas
            .Where(c => c.FechaVencimiento != null && c.FechaVencimiento < hoy
                && (c.Estado == EstadoCuota.PENDIENTE || c.Estado == EstadoCuota.PARCIAL))
            .ToListAsync();
        ViewBag.CantidadCuotasVencidas = cuotasVencidas.Count;
        ViewBag.MontoCuotasVencidas = cuotasVencidas.Sum(c => c.SaldoPendiente);

        ViewBag.TotalPorCobrar = await _context.Cuotas
            .Where(c => c.Estado == EstadoCuota.PENDIENTE || c.Estado == EstadoCuota.PARCIAL)
            .SumAsync(c => c.Monto - c.MontoPagado);

        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        ViewBag.IngresosMes = await _context.AbonosCuota
            .Where(a => a.FechaAbono >= inicioMes)
            .SumAsync(a => (decimal?)a.Monto) ?? 0m;

        ViewBag.ProcesosActivos = await _context.Procesos
            .CountAsync(p => p.Estado == EstadoProceso.NUEVO || p.Estado == EstadoProceso.EN_PROCESO || p.Estado == EstadoProceso.EN_ESPERA);

        // Las solicitudes de cambio pendientes solo las resuelve un abogado,
        // así que solo a ellos les sirve verlo en su propio inicio.
        if (esAbogado)
        {
            ViewBag.SolicitudesPendientes = await _context.SolicitudesCambio
                .CountAsync(s => s.Estado == EstadoSolicitud.PENDIENTE);
        }

        // --- Paneles de actividad reciente (igual que antes) ---
        ViewBag.PagosPendientes = await _context.Pagos
            .Include(p => p.Cliente)
            .Include(p => p.UsuarioRegistro)
            .Where(p => p.Estado == EstadoPago.PENDIENTE)
            .OrderByDescending(p => p.FechaRegistro)
            .Take(LimitePanel)
            .ToListAsync();

        ViewBag.ProximasCitas = await _context.Agenda
            .Include(a => a.Cliente)
            .Where(a => a.Fecha >= hoy && a.Tipo == TipoAgenda.CITA)
            .OrderBy(a => a.Fecha).ThenBy(a => a.Hora)
            .Take(LimitePanel)
            .ToListAsync();

        ViewBag.ProximasAudiencias = await _context.Agenda
            .Include(a => a.Cliente)
            .Where(a => a.Fecha >= hoy && a.Tipo == TipoAgenda.AUDIENCIA)
            .OrderBy(a => a.Fecha).ThenBy(a => a.Hora)
            .Take(LimitePanel)
            .ToListAsync();

        ViewBag.MensajesRecientes = await _context.Mensajes
            .Include(m => m.Remitente)
            .Where(m => m.DestinatarioId == usuarioActual.Id)
            .OrderByDescending(m => m.FechaEnvio)
            .Take(LimitePanel)
            .ToListAsync();

        ViewBag.NotificacionesRecientes = await _context.Notificaciones
            .Where(n => n.UsuarioId == usuarioActual.Id)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(LimitePanel)
            .ToListAsync();

        ViewData["Title"] = "Inicio";
        return View();
    }

    // GET /app/mi-perfil
    [HttpGet("/app/mi-perfil")]
    public async Task<IActionResult> MiPerfil()
    {
        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        ViewData["Title"] = "Mi perfil";
        return View(usuarioActual);
    }

    // POST /app/mi-perfil/password — antes no existía ninguna forma de
    // cambiar la contraseña sin pasar por "Olvidé mi contraseña".
    [HttpPost("/app/mi-perfil/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarPassword(string passwordActual, string passwordNueva, string passwordConfirmacion)
    {
        if (string.IsNullOrEmpty(passwordNueva) || passwordNueva.Length < 8)
        {
            TempData["Error"] = "La nueva contraseña debe tener al menos 8 caracteres.";
            return RedirectToAction(nameof(MiPerfil));
        }

        if (passwordNueva != passwordConfirmacion)
        {
            TempData["Error"] = "La confirmación no coincide con la nueva contraseña.";
            return RedirectToAction(nameof(MiPerfil));
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        var usuario = await _context.Usuarios.FindAsync(usuarioActual.Id);
        if (usuario == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(passwordActual, usuario.Password))
        {
            TempData["Error"] = "La contraseña actual no es correcta.";
            return RedirectToAction(nameof(MiPerfil));
        }

        usuario.Password = BCrypt.Net.BCrypt.HashPassword(passwordNueva);
        usuario.FechaActualizacion = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Mensaje"] = "Tu contraseña se actualizó correctamente.";
        return RedirectToAction(nameof(MiPerfil));
    }

    // Antes devolvía Content("Ocurrió un error.") como texto plano, sin
    // estilos ni forma de volver al sistema, y como el controlador entero
    // exige [Authorize], si la sesión ya había expirado cuando ocurrió el
    // error, esta misma página terminaba redirigiendo al login en vez de
    // mostrar el error.
    [AllowAnonymous]
    [Route("/Home/Error")]
    public IActionResult Error()
    {
        ViewData["Title"] = "Ocurrió un error";
        Response.StatusCode = 500;
        return View();
    }
}
