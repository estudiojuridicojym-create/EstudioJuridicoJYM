using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Util;

// Antes ningún listado (Clientes, Procesos, Pagos) tenía paginación: cada
// pantalla traía la tabla completa de la base de datos y la mandaba entera
// al navegador. Con pocos registros no se nota, pero un estudio activo
// acumula cientos de clientes y procesos rápido, y eso vuelve cada vez más
// lenta la pantalla que más se usa en el día a día.
public class PaginaResultado<T>
{
    public List<T> Items { get; init; } = new();
    public int Pagina { get; init; }
    public int TotalPaginas { get; init; }
    public int TotalRegistros { get; init; }

    public bool TienePaginaAnterior => Pagina > 1;
    public bool TienePaginaSiguiente => Pagina < TotalPaginas;

    public static async Task<PaginaResultado<T>> CrearAsync(IQueryable<T> query, int pagina, int tamañoPagina)
    {
        if (pagina < 1) pagina = 1;

        var total = await query.CountAsync();
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamañoPagina));
        if (pagina > totalPaginas) pagina = totalPaginas;

        var items = await query
            .Skip((pagina - 1) * tamañoPagina)
            .Take(tamañoPagina)
            .ToListAsync();

        return new PaginaResultado<T>
        {
            Items = items,
            Pagina = pagina,
            TotalPaginas = totalPaginas,
            TotalRegistros = total,
        };
    }
}
