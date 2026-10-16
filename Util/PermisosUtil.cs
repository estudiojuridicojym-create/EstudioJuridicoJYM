using System.Security.Claims;

namespace DashboardJym.Util;

// Punto unico donde se decide que puede hacer cada rol.
// ABOGADO  -> control total: edita, desactiva, confirma pagos y aprueba
//             las propuestas de cambio que envian los asesores.
// ASESOR_LEGAL -> registra y consulta; sus ediciones y desactivaciones
//             quedan como solicitud pendiente hasta que un abogado apruebe.
public static class PermisosUtil
{
    public const string RolAbogado = "ABOGADO";
    public const string RolAsesor = "ASESOR_LEGAL";

    public static bool EsAbogado(ClaimsPrincipal? usuario)
        => usuario?.IsInRole(RolAbogado) == true;

    public static bool EsAsesor(ClaimsPrincipal? usuario)
        => usuario?.IsInRole(RolAsesor) == true && !EsAbogado(usuario);
}
