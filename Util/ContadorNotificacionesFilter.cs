using DashboardJym.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Util;

public class ContadorNotificacionesFilter : IAsyncActionFilter
{
    private readonly AppDbContext _context;

    public ContadorNotificacionesFilter(AppDbContext context)
    {
        _context = context;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var correo = context.HttpContext.User.Identity?.IsAuthenticated == true
            ? context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            : null;

        if (correo != null)
        {
            var noLeidas = await _context.Notificaciones
                .Where(n => n.Usuario!.Correo == correo && !n.Leida)
                .CountAsync();

            if (context.Controller is Controller controller)
            {
                controller.ViewData["NotificacionesNoLeidas"] = noLeidas;

                // Solo los abogados ven la bandeja de aprobaciones.
                if (context.HttpContext.User.IsInRole(PermisosUtil.RolAbogado))
                {
                    controller.ViewData["SolicitudesPendientes"] = await _context.SolicitudesCambio
                        .CountAsync(s => s.Estado == Models.EstadoSolicitud.PENDIENTE);
                }
            }
        }

        await next();
    }
}
