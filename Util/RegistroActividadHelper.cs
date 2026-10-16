using DashboardJym.Data;
using DashboardJym.Models;

namespace DashboardJym.Util;

// Solo agrega entradas al historial de auditoria; nunca edita ni elimina.
// Cada modulo (Clientes, Procesos, Pagos, Agenda, Mensajes) llama a
// RegistrarAsync automaticamente cuando ocurre una accion relevante.
public class RegistroActividadHelper
{
    private readonly AppDbContext _context;

    public RegistroActividadHelper(AppDbContext context)
    {
        _context = context;
    }

    public async Task RegistrarAsync(long usuarioId, string accion, string modulo, long? registroId, string descripcion)
    {
        _context.RegistrosActividad.Add(new RegistroActividad
        {
            UsuarioId = usuarioId,
            Accion = accion,
            Modulo = modulo,
            RegistroId = registroId,
            Descripcion = descripcion,
            FechaHora = DateTime.Now,
        });
        await _context.SaveChangesAsync();
    }
}
