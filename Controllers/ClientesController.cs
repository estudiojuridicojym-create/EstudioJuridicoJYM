using DashboardJym.Data;
using DashboardJym.Models;
using DashboardJym.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Controllers;

[Authorize]
[Route("app/clientes")]
public class ClientesController : Controller
{
    private readonly AppDbContext _context;
    private readonly SesionUtil _sesionUtil;
    private readonly RegistroActividadHelper _registroActividad;
    private readonly SolicitudCambioHelper _solicitudCambio;
    private readonly ILogger<ClientesController> _logger;

    public ClientesController(AppDbContext context, SesionUtil sesionUtil,
        RegistroActividadHelper registroActividad, SolicitudCambioHelper solicitudCambio,
        ILogger<ClientesController> logger)
    {
        _context = context;
        _sesionUtil = sesionUtil;
        _registroActividad = registroActividad;
        _solicitudCambio = solicitudCambio;
        _logger = logger;
    }

    private const int TamañoPagina = 20;

    // GET /app/clientes?q=...
    [HttpGet]
    public async Task<IActionResult> Listar(string? q, int pagina = 1)
    {
        var query = _context.Clientes.Where(c => c.Estado);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var texto = q.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Nombres, $"%{texto}%") ||
                EF.Functions.ILike(c.Apellidos, $"%{texto}%") ||
                c.Dni.Contains(texto));
        }

        ViewData["Title"] = "Clientes";
        ViewData["Q"] = q;

        var resultado = await PaginaResultado<Cliente>.CrearAsync(
            query.OrderBy(c => c.Apellidos), pagina, TamañoPagina);

        ViewData["Pagina"] = resultado;
        ViewData["RouteValuesExtra"] = new { q };
        return View(resultado.Items);
    }

    // GET /app/clientes/nuevo
    [HttpGet("nuevo")]
    public IActionResult FormularioNuevo()
    {
        ViewData["Title"] = "Registrar cliente";
        ViewData["EsNuevo"] = true;
        return View("Formulario", new Cliente());
    }

    // POST /app/clientes
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(Cliente cliente)
    {
        // Solo se toman los campos del formulario simplificado; el resto
        // (usuario, estado, fechas) se completa aquí, igual que en el service Java.
        // Antes, un DNI duplicado no se detectaba aquí: la restricción UNIQUE
        // de la base de datos lo rechazaba y el usuario veía una página de
        // error 500 en vez de un mensaje claro sobre qué corregir.
        if (await _context.Clientes.AnyAsync(c => c.Dni == cliente.Dni))
        {
            ModelState.AddModelError(nameof(Cliente.Dni), "Ya existe un cliente registrado con ese DNI.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Registrar cliente";
            ViewData["EsNuevo"] = true;
            return View("Formulario", cliente);
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        cliente.UsuarioRegistroId = usuarioActual.Id;
        cliente.Estado = true;
        cliente.FechaRegistro = DateTime.Now;
        cliente.FechaActualizacion = DateTime.Now;

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Registró cliente", "Clientes", cliente.Id,
            $"Registró al cliente {cliente.Nombres} {cliente.Apellidos}");

        TempData["Mensaje"] = "Cliente registrado correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    // GET /app/clientes/{id}/editar
    [HttpGet("{id:long}/editar")]
    public async Task<IActionResult> FormularioEditar(long id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null) return NotFound();

        ViewData["Title"] = "Editar cliente";
        ViewData["EsNuevo"] = false;
        return View("Formulario", cliente);
    }

    // POST /app/clientes/{id}
    [HttpPost("{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(long id, Cliente datos)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null) return NotFound();

        // Mismo control que al registrar: evita el error 500 de la
        // restricción UNIQUE si el DNI editado ya pertenece a otro cliente.
        if (await _context.Clientes.AnyAsync(c => c.Dni == datos.Dni && c.Id != id))
        {
            ModelState.AddModelError(nameof(Cliente.Dni), "Ya existe otro cliente registrado con ese DNI.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar cliente";
            ViewData["EsNuevo"] = false;
            datos.Id = id;
            return View("Formulario", datos);
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        // Un asesor legal no escribe sobre el registro: su edicion queda
        // como propuesta y solo se aplica cuando un abogado la aprueba.
        if (PermisosUtil.EsAsesor(User))
        {
            datos.Id = id;
            var resumen = SolicitudCambioHelper.ResumirCambios(cliente, datos,
                nameof(Cliente.Nombres), nameof(Cliente.Apellidos), nameof(Cliente.Dni),
                nameof(Cliente.FechaEmisionDni), nameof(Cliente.FechaNacimiento),
                nameof(Cliente.NombrePadre), nameof(Cliente.NombreMadre),
                nameof(Cliente.Whatsapp), nameof(Cliente.Correo), nameof(Cliente.Observaciones));

            try
            {
                await _solicitudCambio.CrearAsync("Clientes", id, TipoAccionSolicitud.EDITAR, datos,
                    $"Editar al cliente {cliente.Nombres} {cliente.Apellidos}", resumen, usuarioActual.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear solicitud de edición de cliente {Id}", id);
                TempData["Mensaje"] = "No se pudo enviar la solicitud. Intenta nuevamente o contacta al administrador.";
                return RedirectToAction(nameof(Listar));
            }

            TempData["Mensaje"] = "Tu solicitud de edición fue enviada. Un abogado debe aprobarla antes de que el cambio se aplique.";
            return RedirectToAction(nameof(Listar));
        }

        // Mismos campos editables que en el service Java: no se toca
        // usuario_registro_id, estado ni fecha_registro.
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
        cliente.FechaActualizacion = DateTime.Now;

        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Editó cliente", "Clientes", cliente.Id,
            $"Editó los datos del cliente {cliente.Nombres} {cliente.Apellidos}");

        TempData["Mensaje"] = "Cliente actualizado correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    // GET /app/clientes/{id}
    [HttpGet("{id:long}")]
    public async Task<IActionResult> VerDetalle(long id)
    {
        var cliente = await _context.Clientes
            .Include(c => c.UsuarioRegistro)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cliente == null) return NotFound();

        ViewData["Title"] = "Detalle de cliente";
        return View("Detalle", cliente);
    }

    // POST /app/clientes/{id}/desactivar
    [HttpPost("{id:long}/desactivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(long id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null) return NotFound();

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        if (PermisosUtil.EsAsesor(User))
        {
            try
            {
                await _solicitudCambio.CrearAsync<Cliente>("Clientes", id, TipoAccionSolicitud.DESACTIVAR, null,
                    $"Desactivar al cliente {cliente.Nombres} {cliente.Apellidos}",
                    "El cliente pasaría a estado inactivo (no se elimina de la base de datos).",
                    usuarioActual.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear solicitud de desactivación de cliente {Id}", id);
                TempData["Mensaje"] = "No se pudo enviar la solicitud. Intenta nuevamente o contacta al administrador.";
                return RedirectToAction(nameof(Listar));
            }

            TempData["Mensaje"] = "Tu solicitud de desactivación fue enviada. Un abogado debe aprobarla.";
            return RedirectToAction(nameof(Listar));
        }

        // Eliminacion logica: el cliente permanece en la BD.
        cliente.Estado = false;
        cliente.FechaActualizacion = DateTime.Now;
        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Desactivó cliente", "Clientes", cliente.Id,
            $"Desactivó al cliente {cliente.Nombres} {cliente.Apellidos}");

        TempData["Mensaje"] = "Cliente desactivado correctamente.";
        return RedirectToAction(nameof(Listar));
    }
}
