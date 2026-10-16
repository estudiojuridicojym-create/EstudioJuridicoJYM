using System.Globalization;
using DashboardJym.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Fuerza la cultura invariante (formato de fecha yyyy-MM-dd, decimales con
// punto) para TODA la app, sin importar la configuracion regional de
// Windows del servidor. Es imprescindible: los <input type="date"> y
// <input type="number"> del navegador siempre envian ese formato, y si el
// servidor interpreta con otra cultura (ej. es-PE, que usa coma decimal),
// el model binding falla en silencio y los formularios guardan datos
// incorrectos o vacios sin mostrar ningun error.
var culturaInvariante = new[] { new CultureInfo("en-US") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = culturaInvariante;
    options.SupportedUICultures = culturaInvariante;
});

// --- MVC con vistas Razor (equivalente a spring-boot-starter-webmvc + thymeleaf) ---
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<DashboardJym.Util.ContadorNotificacionesFilter>();
});

// --- Conexion a PostgreSQL (equivalente a spring.datasource.* + spring.jpa.*) ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// --- Autenticacion por cookies (equivalente al login por sesion de Spring Security) ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/logout";
        // Antes apuntaba a /auth/login: un usuario YA logueado al que le
        // falta un rol (ej. un asesor entrando a /app/solicitudes) terminaba
        // de vuelta en el login, como si no hubiera iniciado sesión.
        options.AccessDeniedPath = "/auth/acceso-denegado";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            // La cookie de sesión dura hasta 8 horas y por defecto ASP.NET
            // Core no vuelve a consultar la base de datos en ese tiempo: si
            // un abogado desactiva a un usuario, ese usuario sigue con
            // acceso completo hasta que la cookie expire por su cuenta.
            // Este validador se ejecuta en cada request autenticado y cierra
            // la sesión de inmediato si el usuario fue desactivado o
            // eliminado después de haber iniciado sesión.
            OnValidatePrincipal = async context =>
            {
                var correo = context.Principal?.Identity?.Name;
                if (string.IsNullOrEmpty(correo))
                {
                    context.RejectPrincipal();
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var sigueActivo = await dbContext.Usuarios
                    .AnyAsync(u => u.Correo == correo && u.Estado);

                if (!sigueActivo)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

builder.Services.AddAuthorization();

// --- Utilidad de sesion (equivalente a util/SesionUtil.java) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DashboardJym.Util.SesionUtil>();
builder.Services.AddScoped<DashboardJym.Util.NotificacionHelper>();
builder.Services.AddScoped<DashboardJym.Util.ContadorNotificacionesFilter>();
builder.Services.AddScoped<DashboardJym.Util.RegistroActividadHelper>();
builder.Services.AddScoped<DashboardJym.Util.SolicitudCambioHelper>();
builder.Services.AddHttpClient(nameof(DashboardJym.Util.EmailHelper));
builder.Services.AddScoped<DashboardJym.Util.EmailHelper>();

// Singleton (no Scoped): necesita mantener el conteo de intentos fallidos
// en memoria durante toda la vida del proceso, compartido entre todas las
// peticiones, no uno nuevo por cada request.
builder.Services.AddSingleton<DashboardJym.Util.LoginAttemptService>();

// Avisa un día antes de cada plazo procesal o audiencia (ver la clase para
// el detalle de por qué es un BackgroundService y no un servicio Scoped).
builder.Services.AddHostedService<DashboardJym.Util.RecordatorioPlazosService>();

var app = builder.Build();

// --- Cabeceras reenviadas por el proxy de Render (equivalente a confiar en
// el load balancer de un hosting como Heroku/Railway) ---
// Render termina el TLS en su borde y le reenvia la peticion a este
// contenedor en texto plano (HTTP), agregando la cabecera
// "X-Forwarded-Proto: https". Sin este middleware, ASP.NET Core cree que
// TODAS las peticiones llegan por HTTP y UseHttpsRedirection() fuerza un
// redirect a https en cada una; el navegador vuelve a pedir https, Render
// vuelve a reenviar por http al contenedor... y se genera un bucle infinito
// de redirects. Ademas, como la IP del proxy de Render no esta en la lista
// de "proxies conocidos" por defecto de ASP.NET Core, hay que vaciar esas
// listas para que la cabecera se acepte venga de donde venga.
var forwardedHeaderOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaderOptions.KnownNetworks.Clear();
forwardedHeaderOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaderOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
