using DashboardJym.Data;
using DashboardJym.Models;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Util;

public class NotificacionHelper
{
    private readonly AppDbContext _context;

    public NotificacionHelper(AppDbContext context)
    {
        _context = context;
    }

    public async Task NotificarAsync(long destinatarioId, string titulo, string mensaje, string tipo, long? referenciaId)
    {
        _context.Notificaciones.Add(new Notificacion
        {
            UsuarioId = destinatarioId,
            Titulo = titulo,
            Mensaje = mensaje,
            Tipo = tipo,
            ReferenciaId = referenciaId,
            Leida = false,
            FechaCreacion = DateTime.Now,
        });
        await _context.SaveChangesAsync();
    }

    // Notifica a todos los usuarios activos excepto quien disparó la acción.
    public async Task NotificarAOtrosAsync(long usuarioQueDisparaId, string titulo, string mensaje, string tipo, long? referenciaId)
    {
        var activos = await _context.Usuarios.Where(u => u.Estado).ToListAsync();
        foreach (var u in activos.Where(u => u.Id != usuarioQueDisparaId))
        {
            _context.Notificaciones.Add(new Notificacion
            {
                UsuarioId = u.Id,
                Titulo = titulo,
                Mensaje = mensaje,
                Tipo = tipo,
                ReferenciaId = referenciaId,
                Leida = false,
                FechaCreacion = DateTime.Now,
            });
        }
        await _context.SaveChangesAsync();
    }
}
