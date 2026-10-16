using System.Security.Claims;
using DashboardJym.Data;
using DashboardJym.Models;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Util;

public class SesionUtil
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SesionUtil(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Usuario> ObtenerUsuarioActualAsync()
    {
        var correo = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.Name);

        if (correo == null)
        {
            throw new InvalidOperationException("No hay un usuario autenticado en la sesión actual.");
        }

        return await _context.Usuarios.FirstAsync(u => u.Correo == correo);
    }
}
