using System.Collections.Concurrent;

namespace DashboardJym.Util;

// Bloqueo temporal de cuenta tras varios intentos fallidos de login
// consecutivos (fuerza bruta / adivinar contraseña). Se registra en
// memoria del proceso, no en la base de datos: es intencional y suficiente
// para un solo contenedor (que es como corre hoy en Render, ver Dockerfile),
// pero se reinicia si el contenedor se reinicia y NO se comparte entre
// varias instancias si el estudio llegara a escalar a más de un contenedor.
// Si eso pasa, esta lógica debería moverse a una tabla en PostgreSQL o a
// un cache compartido (Redis).
public class LoginAttemptService
{
    private const int MaxIntentos = 5;
    private static readonly TimeSpan VentanaReinicio = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    private class Estado
    {
        public int Intentos;
        public DateTime UltimoIntento;
        public DateTime? BloqueadoHasta;
    }

    private readonly ConcurrentDictionary<string, Estado> _estados = new();

    private static string Normalizar(string correo) => correo.Trim().ToLowerInvariant();

    // true si la cuenta está bloqueada ahora mismo; tiempoRestante indica
    // cuánto falta para que pueda volver a intentar.
    public bool EstaBloqueado(string correo, out TimeSpan tiempoRestante)
    {
        tiempoRestante = TimeSpan.Zero;
        var clave = Normalizar(correo);

        if (_estados.TryGetValue(clave, out var estado) && estado.BloqueadoHasta.HasValue)
        {
            var ahora = DateTime.Now;
            if (estado.BloqueadoHasta.Value > ahora)
            {
                tiempoRestante = estado.BloqueadoHasta.Value - ahora;
                return true;
            }

            // El bloqueo ya venció: se limpia el estado para que el próximo
            // intento arranque en cero en vez de quedar "casi bloqueado".
            _estados.TryRemove(clave, out _);
        }

        return false;
    }

    public void RegistrarFallo(string correo)
    {
        var clave = Normalizar(correo);
        var ahora = DateTime.Now;

        var estado = _estados.AddOrUpdate(clave,
            _ => new Estado { Intentos = 1, UltimoIntento = ahora },
            (_, existente) =>
            {
                // Si el último intento fallido fue hace rato, no se acumulan
                // intentos viejos: se reinicia el contador.
                if (ahora - existente.UltimoIntento > VentanaReinicio)
                {
                    existente.Intentos = 0;
                }

                existente.Intentos++;
                existente.UltimoIntento = ahora;
                return existente;
            });

        if (estado.Intentos >= MaxIntentos)
        {
            estado.BloqueadoHasta = ahora.Add(DuracionBloqueo);
        }
    }

    // Login correcto: se olvida cualquier intento fallido previo.
    public void RegistrarExito(string correo)
    {
        _estados.TryRemove(Normalizar(correo), out _);
    }
}
