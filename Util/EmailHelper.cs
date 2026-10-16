using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DashboardJym.Util;

// Envia correos usando la API HTTP de Brevo (antes Sendinblue), en vez de
// SMTP directo. Render bloquea el trafico saliente por los puertos SMTP
// (25, 465, 587) en sus servicios gratuitos, asi que un SmtpClient normal
// nunca logra conectar ahi ("Network is unreachable"); la API de Brevo
// viaja por HTTPS (puerto 443), que si esta permitido.
//
// Configuracion esperada en appsettings.json / variables de entorno,
// seccion "Brevo":
//   Brevo:ApiKey            -> API key generada en Brevo (SMTP & API > API Keys)
//   Brevo:RemitenteEmail    -> correo verificado en Brevo (de tu dominio propio)
//   Brevo:RemitenteNombre   -> nombre a mostrar como remitente (opcional)
public class EmailHelper
{
    private const string BrevoEndpoint = "https://api.brevo.com/v3/smtp/email";

    private readonly IConfiguration _configuracion;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EmailHelper> _logger;

    public EmailHelper(IConfiguration configuracion, IHttpClientFactory httpClientFactory, ILogger<EmailHelper> logger)
    {
        _configuracion = configuracion;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpoHtml)
    {
        var apiKey = _configuracion["Brevo:ApiKey"];
        var remitenteEmail = _configuracion["Brevo:RemitenteEmail"];
        var remitenteNombre = _configuracion["Brevo:RemitenteNombre"] ?? "Estudio Jurídico JYM";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(remitenteEmail))
        {
            // Falta configurar Brevo en appsettings/variables de entorno.
            // Se registra en el log en vez de lanzar una excepcion que
            // exponga detalles al usuario final.
            _logger.LogError("No se pudo enviar el correo a {Destinatario}: falta configurar Brevo:ApiKey o Brevo:RemitenteEmail.", destinatario);
            throw new InvalidOperationException("El servidor de correo no está configurado.");
        }

        var payload = new
        {
            sender = new { name = remitenteNombre, email = remitenteEmail },
            to = new[] { new { email = destinatario } },
            subject = asunto,
            htmlContent = cuerpoHtml,
        };

        using var contenido = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var cliente = _httpClientFactory.CreateClient(nameof(EmailHelper));
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, BrevoEndpoint) { Content = contenido };
        solicitud.Headers.Add("api-key", apiKey);
        solicitud.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var respuesta = await cliente.SendAsync(solicitud);

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync();
            _logger.LogError(
                "Error enviando correo a {Destinatario} vía Brevo. Código {Codigo}. Detalle: {Detalle}",
                destinatario, (int)respuesta.StatusCode, detalle);
            throw new InvalidOperationException("No se pudo enviar el correo de recuperación.");
        }
    }
}
