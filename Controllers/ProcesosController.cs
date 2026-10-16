using DashboardJym.Data;
using DashboardJym.Models;
using DashboardJym.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Controllers;

[Authorize]
[Route("app/procesos")]
public class ProcesosController : Controller
{
    private readonly AppDbContext _context;
    private readonly SesionUtil _sesionUtil;
    private readonly RegistroActividadHelper _registroActividad;
    private readonly SolicitudCambioHelper _solicitudCambio;
    private readonly ILogger<ProcesosController> _logger;

    public ProcesosController(AppDbContext context, SesionUtil sesionUtil,
        RegistroActividadHelper registroActividad, SolicitudCambioHelper solicitudCambio,
        ILogger<ProcesosController> logger)
    {
        _context = context;
        _sesionUtil = sesionUtil;
        _registroActividad = registroActividad;
        _solicitudCambio = solicitudCambio;
        _logger = logger;
    }

    private const int TamañoPagina = 20;

    // GET /app/procesos?q=...&clienteId=...
    [HttpGet]
    public async Task<IActionResult> Listar(string? q, long? clienteId, int pagina = 1)
    {
        var query = _context.Procesos
            .Include(p => p.Cliente)
            .Include(p => p.TipoProceso)
            .Include(p => p.AsesorResponsable)
            .Include(p => p.AbogadoResponsable)
            .AsQueryable();

        if (clienteId.HasValue)
        {
            query = query.Where(p => p.ClienteId == clienteId.Value)
                         .OrderByDescending(p => p.FechaRegistro);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var texto = q.Trim();
                query = query.Where(p =>
                    EF.Functions.ILike(p.Materia, $"%{texto}%") ||
                    EF.Functions.ILike(p.Cliente!.Nombres, $"%{texto}%") ||
                    EF.Functions.ILike(p.Cliente!.Apellidos, $"%{texto}%") ||
                    (p.NumeroExpediente != null && EF.Functions.ILike(p.NumeroExpediente, $"%{texto}%")) ||
                    (p.NumeroCarpeta != null && EF.Functions.ILike(p.NumeroCarpeta, $"%{texto}%")));
            }
            query = query.OrderByDescending(p => p.FechaRegistro);
        }

        ViewData["Title"] = "Procesos";
        ViewData["Q"] = q;

        var resultado = await PaginaResultado<Proceso>.CrearAsync(query, pagina, TamañoPagina);
        ViewData["Pagina"] = resultado;
        ViewData["RouteValuesExtra"] = new { q, clienteId };
        return View(resultado.Items);
    }

    // GET /app/procesos/nuevo?clienteId=...
    [HttpGet("nuevo")]
    public async Task<IActionResult> FormularioNuevo(long? clienteId)
    {
        var proceso = new Proceso();
        if (clienteId.HasValue)
        {
            proceso.ClienteId = clienteId.Value;
        }

        ViewData["Title"] = "Registrar proceso";
        ViewData["EsNuevo"] = true;
        await CargarListasApoyoAsync();
        return View("Formulario", proceso);
    }

    // POST /app/procesos
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(Proceso proceso)
    {
        ModelState.Remove(nameof(Proceso.Cliente));
        ModelState.Remove(nameof(Proceso.TipoProceso));
        ModelState.Remove(nameof(Proceso.AbogadoResponsable));
        ModelState.Remove(nameof(Proceso.AsesorResponsable));
        ModelState.Remove(nameof(Proceso.UsuarioRegistro));

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Registrar proceso";
            ViewData["EsNuevo"] = true;
            await CargarListasApoyoAsync();
            return View("Formulario", proceso);
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        proceso.UsuarioRegistroId = usuarioActual.Id;
        // Un proceso siempre nace en NUEVO, sin importar qué haya elegido
        // el formulario: evita que se registre directamente como
        // FINALIZADO/ARCHIVADO saltándose el seguimiento intermedio.
        proceso.Estado = EstadoProceso.NUEVO;
        proceso.FechaRegistro = DateTime.Now;
        proceso.FechaActualizacion = DateTime.Now;

        _context.Procesos.Add(proceso);
        await _context.SaveChangesAsync();

        var cliente = await _context.Clientes.FindAsync(proceso.ClienteId);
        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Registró proceso", "Procesos", proceso.Id,
            $"Registró el proceso \"{proceso.Materia}\" del cliente {cliente?.Nombres} {cliente?.Apellidos}");

        TempData["Mensaje"] = "Proceso registrado correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    // GET /app/procesos/{id}/editar
    [HttpGet("{id:long}/editar")]
    public async Task<IActionResult> FormularioEditar(long id)
    {
        var proceso = await _context.Procesos
            .Include(p => p.Cliente)
            .Include(p => p.TipoProceso)
            .Include(p => p.AsesorResponsable)
            .Include(p => p.AbogadoResponsable)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (proceso == null) return NotFound();

        ViewData["Title"] = "Editar proceso";
        ViewData["EsNuevo"] = false;
        await CargarListasApoyoAsync();
        return View("Formulario", proceso);
    }

    // POST /app/procesos/{id}
    [HttpPost("{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(long id, Proceso datos)
    {
        ModelState.Remove(nameof(Proceso.Cliente));
        ModelState.Remove(nameof(Proceso.TipoProceso));
        ModelState.Remove(nameof(Proceso.AbogadoResponsable));
        ModelState.Remove(nameof(Proceso.AsesorResponsable));
        ModelState.Remove(nameof(Proceso.UsuarioRegistro));

        var proceso = await _context.Procesos.FindAsync(id);
        if (proceso == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar proceso";
            ViewData["EsNuevo"] = false;
            datos.Id = id;
            // Se recargan los responsables para que, si alguno fue desactivado, el
            // formulario lo siga mostrando y no se desasigne sin querer al reenviar.
            if (datos.AsesorResponsableId.HasValue)
                datos.AsesorResponsable = await _context.Usuarios.FindAsync(datos.AsesorResponsableId.Value);
            if (datos.AbogadoResponsableId.HasValue)
                datos.AbogadoResponsable = await _context.Usuarios.FindAsync(datos.AbogadoResponsableId.Value);
            await CargarListasApoyoAsync();
            return View("Formulario", datos);
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        if (PermisosUtil.EsAsesor(User))
        {
            datos.Id = id;
            // IMPORTANTE: esta lista debe incluir TODOS los campos que
            // AplicarCambioAsync() vuelca sobre el registro real al aprobar
            // (ver SolicitudesCambioController). Antes faltaban ClienteId,
            // TipoProcesoId, AbogadoResponsableId y AsesorResponsableId: un
            // asesor podía reasignar el cliente o el abogado del caso y el
            // abogado que aprobaba no lo veía en el resumen.
            var resumen = SolicitudCambioHelper.ResumirCambios(proceso, datos,
                nameof(Proceso.ClienteId), nameof(Proceso.TipoProcesoId),
                nameof(Proceso.Materia), nameof(Proceso.Descripcion), nameof(Proceso.UltimoActuado),
                nameof(Proceso.NumeroExpediente), nameof(Proceso.NumeroCarpeta), nameof(Proceso.JuzgadoFiscalia),
                nameof(Proceso.EspecialistaLegal), nameof(Proceso.DistritoJudicial), nameof(Proceso.FechaInicio),
                nameof(Proceso.Estado), nameof(Proceso.Prioridad),
                nameof(Proceso.AbogadoResponsableId), nameof(Proceso.AsesorResponsableId),
                nameof(Proceso.Observaciones));

            try
            {
                await _solicitudCambio.CrearAsync("Procesos", id, TipoAccionSolicitud.EDITAR, datos,
                    $"Editar el proceso \"{proceso.Materia}\"", resumen, usuarioActual.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear solicitud de edición de proceso {Id}", id);
                TempData["Mensaje"] = "No se pudo enviar la solicitud. Intenta nuevamente o contacta al administrador.";
                return RedirectToAction(nameof(Listar));
            }

            TempData["Mensaje"] = "Tu solicitud de edición fue enviada. Un abogado debe aprobarla antes de que el cambio se aplique.";
            return RedirectToAction(nameof(Listar));
        }

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

        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Editó proceso", "Procesos", proceso.Id,
            $"Editó el proceso \"{proceso.Materia}\"");

        TempData["Mensaje"] = "Proceso actualizado correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    // GET /app/procesos/{id}
    [HttpGet("{id:long}")]
    public async Task<IActionResult> VerDetalle(long id)
    {
        var proceso = await _context.Procesos
            .Include(p => p.Cliente)
            .Include(p => p.TipoProceso)
            .Include(p => p.AbogadoResponsable)
            .Include(p => p.AsesorResponsable)
            .Include(p => p.UsuarioRegistro)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proceso == null) return NotFound();

        ViewBag.Actuaciones = await _context.ActuacionesProceso
            .Include(a => a.UsuarioRegistro)
            .Where(a => a.ProcesoId == id)
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.FechaRegistro)
            .ToListAsync();

        ViewData["Title"] = "Detalle de proceso";
        return View("Detalle", proceso);
    }

    // POST /app/procesos/{id}/actuaciones
    // Registrar una actuación es un simple apunte de cronología (como el
    // registro de actividades): cualquiera con acceso al proceso puede
    // agregarla, y queda ahí para siempre — no se edita ni se borra, para
    // que la cronología del expediente sea confiable.
    [HttpPost("{id:long}/actuaciones")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarActuacion(long id, DateTime fecha, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            TempData["Mensaje"] = "Describe la actuación antes de guardarla.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        var proceso = await _context.Procesos.FindAsync(id);
        if (proceso == null) return NotFound();

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        _context.ActuacionesProceso.Add(new ActuacionProceso
        {
            ProcesoId = id,
            Fecha = fecha == default ? DateTime.Today : fecha,
            Descripcion = descripcion.Trim(),
            UsuarioRegistroId = usuarioActual.Id,
            FechaRegistro = DateTime.Now,
        });

        // El campo de texto suelto se mantiene sincronizado con la última
        // actuación para no romper nada que ya dependa de él (ej. el
        // resumen de procesos en otras vistas).
        proceso.UltimoActuado = descripcion.Trim();
        proceso.FechaActualizacion = DateTime.Now;

        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Registró actuación", "Procesos", id,
            $"Agregó una actuación al expediente: {descripcion.Trim()}");

        TempData["Mensaje"] = "Actuación registrada correctamente.";
        return RedirectToAction(nameof(VerDetalle), new { id });
    }

    private async Task CargarListasApoyoAsync()
    {
        ViewBag.Clientes = await _context.Clientes.Where(c => c.Estado).OrderBy(c => c.Apellidos).ToListAsync();

        // Se deduplica por nombre en memoria: puede haber tipos de proceso
        // repetidos cargados históricamente en la tabla y no queremos que
        // se vean duplicados en el selector.
        var tipos = await _context.TiposProceso.Where(t => t.Estado).OrderBy(t => t.Nombre).ToListAsync();
        ViewBag.TiposProceso = tipos
            .GroupBy(t => t.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(t => t.Nombre)
            .ToList();

        var usuarios = await _context.Usuarios
            .Where(u => u.Estado)
            .Include(u => u.UsuarioRoles).ThenInclude(ur => ur.Rol)
            .OrderBy(u => u.Nombres)
            .ToListAsync();
        ViewBag.Asesores = usuarios.Where(u => u.UsuarioRoles.Any(ur => ur.Rol.Nombre == "ASESOR_LEGAL")).ToList();
        ViewBag.Abogados = usuarios.Where(u => u.UsuarioRoles.Any(ur => ur.Rol.Nombre == "ABOGADO")).ToList();

        // Juzgados/fiscalías ya usados en procesos existentes, sin repetir,
        // para sugerirlos como autocompletado en vez de repetir texto libre.
        ViewBag.Juzgados = await _context.Procesos
            .Where(p => p.JuzgadoFiscalia != null && p.JuzgadoFiscalia != "")
            .Select(p => p.JuzgadoFiscalia!.Trim())
            .Distinct()
            .OrderBy(j => j)
            .ToListAsync();

        ViewBag.Estados = Enum.GetValues<EstadoProceso>();
        ViewBag.Prioridades = Enum.GetValues<PrioridadProceso>();
    }
}
