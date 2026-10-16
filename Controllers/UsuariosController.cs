using DashboardJym.Data;
using DashboardJym.Models;
using DashboardJym.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Controllers;

// Pantalla que antes no existía: antes la única forma de "desactivar" o
// "cambiar el rol" de alguien era manipulando la base de datos a mano.
// Solo un abogado administra cuentas.
[Authorize(Roles = PermisosUtil.RolAbogado)]
[Route("app/usuarios")]
public class UsuariosController : Controller
{
    private readonly AppDbContext _context;
    private readonly SesionUtil _sesionUtil;
    private readonly RegistroActividadHelper _registroActividad;

    public UsuariosController(AppDbContext context, SesionUtil sesionUtil, RegistroActividadHelper registroActividad)
    {
        _context = context;
        _sesionUtil = sesionUtil;
        _registroActividad = registroActividad;
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var usuarios = await _context.Usuarios
            .Include(u => u.UsuarioRoles).ThenInclude(ur => ur.Rol)
            .OrderBy(u => u.Apellidos).ThenBy(u => u.Nombres)
            .ToListAsync();

        ViewData["Title"] = "Usuarios";
        return View(usuarios);
    }

    // POST /app/usuarios/{id}/estado — activar o desactivar una cuenta.
    [HttpPost("{id:long}/estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(long id)
    {
        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        if (id == usuarioActual.Id)
        {
            TempData["Mensaje"] = "No puedes desactivar tu propia cuenta.";
            return RedirectToAction(nameof(Listar));
        }

        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();

        // No se puede dejar al estudio sin ningún abogado activo: sin uno,
        // nadie podría confirmar pagos, aprobar solicitudes ni volver a
        // activar cuentas.
        if (usuario.Estado && await EsElUltimoAbogadoActivoAsync(usuario))
        {
            TempData["Mensaje"] = "No puedes desactivar a este usuario: es el único abogado activo que queda.";
            return RedirectToAction(nameof(Listar));
        }

        usuario.Estado = !usuario.Estado;
        usuario.FechaActualizacion = DateTime.Now;
        await _context.SaveChangesAsync();

        var accion = usuario.Estado ? "activó" : "desactivó";
        await _registroActividad.RegistrarAsync(usuarioActual.Id, $"{(usuario.Estado ? "Activó" : "Desactivó")} usuario", "Usuarios", usuario.Id,
            $"{accion} la cuenta de {usuario.NombreCompleto} ({usuario.Correo})");

        TempData["Mensaje"] = $"Cuenta {(usuario.Estado ? "activada" : "desactivada")} correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    // POST /app/usuarios/{id}/rol — cambia entre ABOGADO y ASESOR_LEGAL.
    [HttpPost("{id:long}/rol")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarRol(long id, string rol)
    {
        if (rol != PermisosUtil.RolAbogado && rol != PermisosUtil.RolAsesor)
        {
            TempData["Mensaje"] = "Rol inválido.";
            return RedirectToAction(nameof(Listar));
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        if (id == usuarioActual.Id)
        {
            TempData["Mensaje"] = "No puedes cambiar tu propio rol.";
            return RedirectToAction(nameof(Listar));
        }

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles).ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null) return NotFound();

        if (rol == PermisosUtil.RolAsesor && await EsElUltimoAbogadoActivoAsync(usuario))
        {
            TempData["Mensaje"] = "No puedes quitarle el rol de abogado: es el único abogado activo que queda.";
            return RedirectToAction(nameof(Listar));
        }

        var nuevoRol = await _context.Roles.FirstAsync(r => r.Nombre == rol);

        // Un usuario tiene un solo rol a la vez en este sistema: se
        // reemplazan todas sus filas en usuarios_roles por la nueva.
        _context.UsuariosRoles.RemoveRange(usuario.UsuarioRoles);
        _context.UsuariosRoles.Add(new UsuarioRol { UsuarioId = usuario.Id, RolId = nuevoRol.Id });
        usuario.FechaActualizacion = DateTime.Now;
        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Cambió rol de usuario", "Usuarios", usuario.Id,
            $"Cambió el rol de {usuario.NombreCompleto} ({usuario.Correo}) a {rol}");

        TempData["Mensaje"] = "Rol actualizado correctamente.";
        return RedirectToAction(nameof(Listar));
    }

    private async Task<bool> EsElUltimoAbogadoActivoAsync(Usuario usuario)
    {
        var esAbogado = usuario.UsuarioRoles.Any(ur => ur.Rol.Nombre == PermisosUtil.RolAbogado);
        if (!esAbogado) return false;

        var otrosAbogadosActivos = await _context.Usuarios
            .Where(u => u.Id != usuario.Id && u.Estado)
            .CountAsync(u => u.UsuarioRoles.Any(ur => ur.Rol.Nombre == PermisosUtil.RolAbogado));

        return otrosAbogadosActivos == 0;
    }
}
