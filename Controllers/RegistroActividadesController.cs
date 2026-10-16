using DashboardJym.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Controllers;

// Solo lectura: el historial de auditoria no se puede crear, editar ni
// eliminar manualmente desde ningun lugar del sistema. Cada entrada la
// generan los demas modulos (Clientes, Procesos, Pagos, Agenda, Mensajes)
// automaticamente a traves de RegistroActividadHelper.
// El historial de auditoría expone toda la actividad de todo el estudio
// (quién editó qué y cuándo); solo un abogado debe poder consultarlo.
[Authorize(Roles = DashboardJym.Util.PermisosUtil.RolAbogado)]
[Route("app/registro-actividades")]
public class RegistroActividadesController : Controller
{
    private static readonly string[] Modulos = { "Clientes", "Procesos", "Pagos", "Agenda", "Mensajes" };

    private readonly AppDbContext _context;

    public RegistroActividadesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(string? modulo, long? usuarioId)
    {
        var query = _context.RegistrosActividad.Include(r => r.Usuario).AsQueryable();

        if (!string.IsNullOrWhiteSpace(modulo))
        {
            query = query.Where(r => r.Modulo == modulo);
        }
        else if (usuarioId.HasValue)
        {
            query = query.Where(r => r.UsuarioId == usuarioId.Value);
        }

        var registros = await query.OrderByDescending(r => r.FechaHora).ToListAsync();

        ViewBag.Modulos = Modulos;
        ViewBag.Usuarios = await _context.Usuarios.OrderBy(u => u.Nombres).ToListAsync();
        ViewBag.ModuloSeleccionado = modulo;
        ViewBag.UsuarioSeleccionado = usuarioId;

        ViewData["Title"] = "Registro de actividades";
        return View("Lista", registros);
    }
}
