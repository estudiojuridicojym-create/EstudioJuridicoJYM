using System.Security.Claims;
using System.Security.Cryptography;
using DashboardJym.Data;
using DashboardJym.Models;
using DashboardJym.Models.ViewModels;
using DashboardJym.Util;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Controllers;

public class AuthController : Controller
{
    // Tiempo de vida del enlace de recuperación de contraseña.
    private static readonly TimeSpan VigenciaTokenReset = TimeSpan.FromMinutes(30);

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuracion;
    private readonly EmailHelper _emailHelper;
    private readonly ILogger<AuthController> _logger;
    private readonly LoginAttemptService _intentosLogin;

    public AuthController(AppDbContext context, IConfiguration configuracion, EmailHelper emailHelper,
        ILogger<AuthController> logger, LoginAttemptService intentosLogin)
    {
        _context = context;
        _configuracion = configuracion;
        _emailHelper = emailHelper;
        _logger = logger;
        _intentosLogin = intentosLogin;
    }

    [HttpGet("/auth/login")]
    [AllowAnonymous]
    public IActionResult Login(bool registrado = false, bool error = false, bool passwordRestablecida = false, int minutosBloqueo = 0)
    {
        ViewData["Registrado"] = registrado;
        ViewData["Error"] = error;
        ViewData["PasswordRestablecida"] = passwordRestablecida;
        ViewData["MinutosBloqueo"] = minutosBloqueo;
        return View(new LoginViewModel());
    }

    // A donde llega un usuario YA logueado que intenta entrar a una sección
    // para la que su rol no alcanza (ej. un asesor abriendo /app/solicitudes).
    // Antes esto reusaba AccessDeniedPath = "/auth/login", así que a un
    // usuario con sesión activa lo mandaba de vuelta al formulario de login,
    // como si no hubiera iniciado sesión.
    [HttpGet("/auth/acceso-denegado")]
    public IActionResult AccesoDenegado() => View();

    [HttpPost("/auth/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // Antes de siquiera consultar la contraseña: si esta cuenta ya
        // acumuló demasiados intentos fallidos recientes, se corta aquí. Esto
        // evita que alguien pruebe contraseñas indefinidamente (fuerza
        // bruta / adivinanza) contra el correo de un usuario real.
        if (_intentosLogin.EstaBloqueado(modelo.Correo, out var tiempoRestante))
        {
            var minutos = Math.Max(1, (int)Math.Ceiling(tiempoRestante.TotalMinutes));
            return RedirectToAction(nameof(Login), new { minutosBloqueo = minutos });
        }

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles).ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.Correo == modelo.Correo);

        // Misma validacion que hacia CustomUserDetailsService: usuario debe
        // existir, estar activo, y la contraseña debe coincidir con el hash.
        if (usuario == null || !usuario.Estado ||
            !BCrypt.Net.BCrypt.Verify(modelo.Password, usuario.Password))
        {
            _intentosLogin.RegistrarFallo(modelo.Correo);
            return RedirectToAction(nameof(Login), new { error = true });
        }

        _intentosLogin.RegistrarExito(modelo.Correo);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Correo),
            new(ClaimTypes.GivenName, usuario.NombreCompleto),
        };
        claims.AddRange(usuario.UsuarioRoles.Select(ur => new Claim(ClaimTypes.Role, ur.Rol.Nombre)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/auth/registro")]
    [AllowAnonymous]
    public IActionResult Registro()
    {
        return View(new RegistroViewModel());
    }

    [HttpPost("/auth/registro")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registro(RegistroViewModel modelo)
    {
        // Bloquea el autorregistro a cualquiera de internet: el codigo debe
        // coincidir con uno de los dos configurados en el servidor
        // ("CodigoInvitacionAbogado" o "CodigoInvitacionAsesor"), que solo
        // conoce el equipo del estudio. El rol NO lo elige quien se registra:
        // lo determina cuál de los dos códigos escribió, evitando que un
        // asesor se autoasigne el rol de abogado usando el código equivocado.
        var codigoAbogado = _configuracion["CodigoInvitacionAbogado"];
        var codigoAsesor = _configuracion["CodigoInvitacionAsesor"];

        string? rolAsignado = null;
        if (!string.IsNullOrEmpty(codigoAbogado) && modelo.CodigoInvitacion == codigoAbogado)
        {
            rolAsignado = PermisosUtil.RolAbogado;
        }
        else if (!string.IsNullOrEmpty(codigoAsesor) && modelo.CodigoInvitacion == codigoAsesor)
        {
            rolAsignado = PermisosUtil.RolAsesor;
        }
        else
        {
            ModelState.AddModelError(nameof(modelo.CodigoInvitacion), "Código de invitación inválido.");
        }

        if (await _context.Usuarios.AnyAsync(u => u.Correo == modelo.Correo))
        {
            ModelState.AddModelError(nameof(modelo.Correo), "Ya existe una cuenta con este correo.");
        }

        if (await _context.Usuarios.AnyAsync(u => u.Dni == modelo.Dni))
        {
            ModelState.AddModelError(nameof(modelo.Dni), "Ya existe una cuenta con este DNI.");
        }

        if (!ModelState.IsValid || rolAsignado == null)
        {
            return View(modelo);
        }

        var rol = await _context.Roles.FirstAsync(r => r.Nombre == rolAsignado);

        var usuario = new Usuario
        {
            Nombres = modelo.Nombres,
            Apellidos = modelo.Apellidos,
            Dni = modelo.Dni,
            Telefono = modelo.Telefono,
            Correo = modelo.Correo,
            Password = BCrypt.Net.BCrypt.HashPassword(modelo.Password),
            Estado = true,
            FechaCreacion = DateTime.Now,
            FechaActualizacion = DateTime.Now,
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        _context.UsuariosRoles.Add(new UsuarioRol { UsuarioId = usuario.Id, RolId = rol.Id });
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Login), new { registrado = true });
    }

    [HttpGet("/auth/olvide-password")]
    [AllowAnonymous]
    public IActionResult OlvidePassword()
    {
        return View(new OlvidoPasswordViewModel());
    }

    [HttpPost("/auth/olvide-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OlvidePassword(OlvidoPasswordViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == modelo.Correo);

        // Si el usuario existe y esta activo, se genera el token y se envia
        // el correo. Si no existe, se muestra igual el mensaje de "enviado"
        // (nunca "correo no encontrado") para no revelar a un atacante qué
        // correos están registrados en el sistema.
        if (usuario != null && usuario.Estado)
        {
            var token = GenerarTokenSeguro();

            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                UsuarioId = usuario.Id,
                Token = token,
                FechaCreacion = DateTime.Now,
                FechaExpiracion = DateTime.Now.Add(VigenciaTokenReset),
                Usado = false,
            });
            await _context.SaveChangesAsync();

            // Antes se armaba con Request.Scheme (que internamente toma el
            // host de la petición). Como AllowedHosts está en "*", alguien
            // podría mandar una petición con un encabezado Host falso y
            // lograr que el correo de recuperación lleve un enlace a un
            // dominio distinto (phishing). Por eso el dominio sale de la
            // configuración del servidor, nunca de la petición entrante.
            var baseUrl = _configuracion["AppBaseUrl"];
            string enlace;
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                enlace = $"{baseUrl.TrimEnd('/')}/auth/restablecer-password?token={Uri.EscapeDataString(token)}";
            }
            else
            {
                // Sin AppBaseUrl configurado (ej. en desarrollo local) se usa
                // el host de la petición como respaldo; en producción debe
                // configurarse siempre AppBaseUrl.
                _logger.LogWarning("AppBaseUrl no está configurado; se usará el host de la petición para el enlace de recuperación.");
                enlace = Url.Action(nameof(RestablecerPassword), "Auth", new { token }, Request.Scheme)!;
            }

            var cuerpo = $@"
                <p>Hola {System.Net.WebUtility.HtmlEncode(usuario.Nombres)},</p>
                <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta en el Panel de Estudio Jurídico JYM.</p>
                <p><a href=""{enlace}"">Haz clic aquí para crear una nueva contraseña</a></p>
                <p>Este enlace vence en 30 minutos. Si tú no solicitaste este cambio, puedes ignorar este correo: tu contraseña actual seguirá funcionando.</p>";

            try
            {
                await _emailHelper.EnviarCorreoAsync(usuario.Correo, "Recupera tu contraseña - Estudio Jurídico JYM", cuerpo);
            }
            catch (Exception ex)
            {
                // No se expone el error al usuario (evita revelar detalles del
                // servidor de correo); queda registrado para revisión.
                _logger.LogError(ex, "Error enviando correo de recuperación de contraseña a {Correo}", usuario.Correo);
            }
        }

        ViewData["CorreoEnviado"] = true;
        return View(new OlvidoPasswordViewModel());
    }

    [HttpGet("/auth/restablecer-password")]
    [AllowAnonymous]
    public async Task<IActionResult> RestablecerPassword(string token)
    {
        var tokenValido = await ObtenerTokenValidoAsync(token);
        if (tokenValido == null)
        {
            return View("TokenInvalido");
        }

        return View(new RestablecerPasswordViewModel { Token = token });
    }

    [HttpPost("/auth/restablecer-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestablecerPassword(RestablecerPasswordViewModel modelo)
    {
        var tokenValido = await ObtenerTokenValidoAsync(modelo.Token);
        if (tokenValido == null)
        {
            return View("TokenInvalido");
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        tokenValido.Usuario.Password = BCrypt.Net.BCrypt.HashPassword(modelo.Password);
        tokenValido.Usuario.FechaActualizacion = DateTime.Now;
        tokenValido.Usado = true;

        // Invalida cualquier otro enlace de recuperación pendiente para este
        // usuario (por ejemplo si pidió el correo varias veces).
        var otrosTokens = await _context.PasswordResetTokens
            .Where(t => t.UsuarioId == tokenValido.UsuarioId && !t.Usado && t.Id != tokenValido.Id)
            .ToListAsync();
        foreach (var otro in otrosTokens)
        {
            otro.Usado = true;
        }

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Login), new { passwordRestablecida = true });
    }

    private async Task<PasswordResetToken?> ObtenerTokenValidoAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenValido = await _context.PasswordResetTokens
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.Token == token);

        if (tokenValido == null || tokenValido.Usado || tokenValido.FechaExpiracion < DateTime.Now
            || !tokenValido.Usuario.Estado)
        {
            return null;
        }

        return tokenValido;
    }

    private static string GenerarTokenSeguro()
    {
        // 32 bytes aleatorios en Base64Url: suficiente entropía para que no
        // se pueda adivinar y seguro de incluir en una URL (sin '+', '/' ni '=').
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }

    [HttpPost("/logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
