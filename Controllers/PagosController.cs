using DashboardJym.Data;
using DashboardJym.Models;
using DashboardJym.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardJym.Controllers;

[Authorize]
[Route("app/pagos")]
public class PagosController : Controller
{
    private readonly AppDbContext _context;
    private readonly SesionUtil _sesionUtil;
    private readonly NotificacionHelper _notificaciones;
    private readonly RegistroActividadHelper _registroActividad;
    private readonly ILogger<PagosController> _logger;

    public PagosController(AppDbContext context, SesionUtil sesionUtil, NotificacionHelper notificaciones,
        RegistroActividadHelper registroActividad, ILogger<PagosController> logger)
    {
        _context = context;
        _sesionUtil = sesionUtil;
        _notificaciones = notificaciones;
        _registroActividad = registroActividad;
        _logger = logger;
    }

    private const int TamañoPagina = 20;

    // GET /app/pagos?pendientes=true&clienteId=...
    [HttpGet]
    public async Task<IActionResult> Listar(bool? pendientes, long? clienteId, int pagina = 1)
    {
        var query = _context.Pagos.Include(p => p.Cliente).AsQueryable();

        if (clienteId.HasValue)
        {
            query = query.Where(p => p.ClienteId == clienteId.Value);
        }
        else if (pendientes == true)
        {
            query = query.Where(p => p.Estado == EstadoPago.PENDIENTE);
        }

        ViewData["Title"] = "Pagos";
        ViewData["Pendientes"] = pendientes;

        var resultado = await PaginaResultado<Pago>.CrearAsync(
            query.OrderByDescending(p => p.FechaRegistro), pagina, TamañoPagina);

        ViewData["Pagina"] = resultado;
        ViewData["RouteValuesExtra"] = new { pendientes, clienteId };
        return View(resultado.Items);
    }

    // GET /app/pagos/nuevo?clienteId=...
    [HttpGet("nuevo")]
    public async Task<IActionResult> FormularioNuevo(long? clienteId)
    {
        var pago = new Pago { FechaPago = DateTime.Today };
        if (clienteId.HasValue) pago.ClienteId = clienteId.Value;

        ViewData["Title"] = "Registrar pago";
        await CargarListasApoyoAsync();
        return View("Formulario", pago);
    }

    // POST /app/pagos
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(Pago pago)
    {
        ModelState.Remove(nameof(Pago.Cliente));
        ModelState.Remove(nameof(Pago.Proceso));
        ModelState.Remove(nameof(Pago.UsuarioRegistro));
        ModelState.Remove(nameof(Pago.UsuarioConfirmacion));
        ModelState.Remove(nameof(Pago.Cuotas));

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Registrar pago";
            await CargarListasApoyoAsync();
            return View("Formulario", pago);
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        pago.UsuarioRegistroId = usuarioActual.Id;
        pago.Estado = EstadoPago.PENDIENTE;
        pago.NumeroCuotas = pago.TipoPago == TipoPago.EXTRA ? 1 : 3;
        pago.FechaRegistro = DateTime.Now;
        pago.FechaActualizacion = DateTime.Now;

        _context.Pagos.Add(pago);
        await _context.SaveChangesAsync();

        // Honorarios: siempre 50% / 25% / 25%. Pago extra (tasas, edictos,
        // gastos externos aparte del honorario): un solo pago, sin dividir.
        if (pago.TipoPago == TipoPago.EXTRA)
        {
            _context.Cuotas.Add(new Cuota
            {
                PagoId = pago.Id,
                NumeroCuota = 1,
                Monto = pago.MontoTotal,
                MontoPagado = 0,
                FechaVencimiento = pago.FechaPago,
                Estado = EstadoCuota.PENDIENTE,
                FormaPago = pago.FormaPago,
            });
        }
        else
        {
            GenerarCuotas(pago);
        }
        await _context.SaveChangesAsync();

        var clientePago = await _context.Clientes.FindAsync(pago.ClienteId);
        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Registró pago", "Pagos", pago.Id,
            $"Registró el pago \"{pago.Concepto}\" por S/ {pago.MontoTotal} del cliente {clientePago?.Nombres} {clientePago?.Apellidos}");

        // Notificar a los demas abogados/asesores para que puedan confirmar u observar.
        await _notificaciones.NotificarAOtrosAsync(
            usuarioActual.Id,
            "Pago pendiente de confirmación",
            $"{usuarioActual.Nombres} registró un pago de S/ {pago.MontoTotal} ({pago.Concepto}) que necesita tu confirmación.",
            "PAGO_PENDIENTE",
            pago.Id);

        TempData["Mensaje"] = "Pago registrado correctamente. Se generaron sus cuotas y se notificó a los demás usuarios.";
        return RedirectToAction(nameof(VerDetalle), new { id = pago.Id });
    }

    // Genera 3 cuotas fijas: 50% al inicio, 25% y 25% despues.
    // La ultima cuota absorbe cualquier centavo de redondeo.
    private void GenerarCuotas(Pago pago)
    {
        decimal cuota1 = Math.Round(pago.MontoTotal * 0.50m, 2, MidpointRounding.ToZero);
        decimal cuota2 = Math.Round(pago.MontoTotal * 0.25m, 2, MidpointRounding.ToZero);
        decimal cuota3 = pago.MontoTotal - cuota1 - cuota2;

        var montos = new[] { cuota1, cuota2, cuota3 };

        for (int i = 0; i < montos.Length; i++)
        {
            _context.Cuotas.Add(new Cuota
            {
                PagoId = pago.Id,
                NumeroCuota = i + 1,
                Monto = montos[i],
                MontoPagado = 0,
                FechaVencimiento = pago.FechaPago.AddMonths(i),
                Estado = EstadoCuota.PENDIENTE,
                FormaPago = pago.FormaPago,
            });
        }
    }

    // GET /app/pagos/{id}
    [HttpGet("{id:long}")]
    public async Task<IActionResult> VerDetalle(long id)
    {
        var pago = await _context.Pagos
            .Include(p => p.Cliente)
            .Include(p => p.Proceso)
            .Include(p => p.UsuarioRegistro)
            .Include(p => p.UsuarioConfirmacion)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pago == null) return NotFound();

        ViewBag.Cuotas = await _context.Cuotas
            .Where(c => c.PagoId == id).OrderBy(c => c.NumeroCuota).ToListAsync();

        ViewBag.Abonos = await _context.AbonosCuota
            .Include(a => a.Cuota)
            .Include(a => a.UsuarioRegistro)
            .Where(a => a.Cuota!.PagoId == id)
            .OrderByDescending(a => a.FechaAbono)
            .ToListAsync();

        // PAGADA y PARCIAL quedan fuera de este selector a propósito: solo se
        // llega a esos estados registrando un abono real (ver RegistrarAbono).
        ViewBag.EstadosCuota = new[] { EstadoCuota.PENDIENTE, EstadoCuota.OBSERVADA, EstadoCuota.ANULADA };
        ViewBag.FormasPago = Enum.GetValues<FormaPago>();

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        ViewBag.EsQuienRegistro = usuarioActual.Id == pago.UsuarioRegistroId;
        ViewBag.PuedeEditar = (await PuedeEditarseAsync(pago)).ok;

        ViewData["Title"] = "Detalle de pago";
        return View("Detalle", pago);
    }

    // POST /app/pagos/{id}/confirmar
    // Confirmar y observar pagos es control financiero: solo ABOGADO.
    [HttpPost("{id:long}/confirmar")]
    [Authorize(Roles = PermisosUtil.RolAbogado)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(long id)
    {
        return await CambiarEstadoPagoAsync(id, EstadoPago.CONFIRMADO, null);
    }

    // POST /app/pagos/{id}/observar
    [HttpPost("{id:long}/observar")]
    [Authorize(Roles = PermisosUtil.RolAbogado)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Observar(long id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Mensaje"] = "Debes indicar el motivo para observar el pago.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }
        return await CambiarEstadoPagoAsync(id, EstadoPago.OBSERVADO, motivo);
    }

    private async Task<IActionResult> CambiarEstadoPagoAsync(long id, EstadoPago nuevoEstado, string? motivoObservacion)
    {
        var pago = await _context.Pagos.FirstOrDefaultAsync(p => p.Id == id);
        if (pago == null) return NotFound();

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        if (pago.Estado != EstadoPago.PENDIENTE)
        {
            TempData["Mensaje"] = $"Este pago ya fue {pago.Estado.ToString().ToLower()}, no se puede volver a procesar.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        if (pago.UsuarioRegistroId == usuarioActual.Id)
        {
            TempData["Mensaje"] = "No puedes confirmar u observar un pago que tú mismo registraste.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        pago.Estado = nuevoEstado;
        pago.UsuarioConfirmacionId = usuarioActual.Id;
        pago.FechaConfirmacion = DateTime.Now;
        pago.FechaActualizacion = DateTime.Now;

        if (motivoObservacion != null)
        {
            var obsPrevia = string.IsNullOrEmpty(pago.Observaciones) ? "" : pago.Observaciones + " | ";
            pago.Observaciones = obsPrevia + "Observación: " + motivoObservacion;
        }

        await _context.SaveChangesAsync();

        var accionRegistro = nuevoEstado == EstadoPago.CONFIRMADO ? "Confirmó pago" : "Observó pago";
        await _registroActividad.RegistrarAsync(usuarioActual.Id, accionRegistro, "Pagos", pago.Id,
            $"{accionRegistro} \"{pago.Concepto}\" por S/ {pago.MontoTotal}" +
                (motivoObservacion != null ? $" — Motivo: {motivoObservacion}" : ""));

        var accionTexto = nuevoEstado == EstadoPago.CONFIRMADO ? "confirmó" : "observó";
        await _notificaciones.NotificarAsync(
            pago.UsuarioRegistroId,
            nuevoEstado == EstadoPago.CONFIRMADO ? "Pago confirmado" : "Pago observado",
            $"{usuarioActual.Nombres} {accionTexto} el pago \"{pago.Concepto}\"." +
                (motivoObservacion != null ? $" Motivo: {motivoObservacion}" : ""),
            $"PAGO_{nuevoEstado}",
            pago.Id);

        TempData["Mensaje"] = nuevoEstado == EstadoPago.CONFIRMADO
            ? "Pago confirmado correctamente."
            : "Pago observado correctamente.";
        return RedirectToAction(nameof(VerDetalle), new { id });
    }

    // GET /app/pagos/{id}/editar
    // Solo se puede editar mientras el pago no fue confirmado y nadie le
    // registró abonos todavía: una vez que hay dinero real de por medio
    // (confirmado o con abonos), cambiar el monto o el tipo de pago
    // rompería la trazabilidad contable, así que a partir de ahí la única
    // salida es anular y registrar uno nuevo.
    [HttpGet("{id:long}/editar")]
    public async Task<IActionResult> Editar(long id)
    {
        var pago = await _context.Pagos.FindAsync(id);
        if (pago == null) return NotFound();

        var puedeEditar = await PuedeEditarseAsync(pago);
        if (!puedeEditar.ok)
        {
            TempData["Mensaje"] = puedeEditar.motivo;
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        ViewData["Title"] = "Editar pago";
        ViewData["EsNuevo"] = false;
        await CargarListasApoyoAsync();
        return View("Formulario", pago);
    }

    // POST /app/pagos/{id}/editar
    [HttpPost("{id:long}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(long id, Pago datos)
    {
        ModelState.Remove(nameof(Pago.Cliente));
        ModelState.Remove(nameof(Pago.Proceso));
        ModelState.Remove(nameof(Pago.UsuarioRegistro));
        ModelState.Remove(nameof(Pago.UsuarioConfirmacion));
        ModelState.Remove(nameof(Pago.Cuotas));

        var pago = await _context.Pagos.FindAsync(id);
        if (pago == null) return NotFound();

        var puedeEditar = await PuedeEditarseAsync(pago);
        if (!puedeEditar.ok)
        {
            TempData["Mensaje"] = puedeEditar.motivo;
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar pago";
            ViewData["EsNuevo"] = false;
            datos.Id = id;
            await CargarListasApoyoAsync();
            return View("Formulario", datos);
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        var eraObservado = pago.Estado == EstadoPago.OBSERVADO;

        pago.ClienteId = datos.ClienteId;
        pago.ProcesoId = datos.ProcesoId;
        pago.Concepto = datos.Concepto;
        pago.MontoTotal = datos.MontoTotal;
        pago.FormaPago = datos.FormaPago;
        pago.TipoPago = datos.TipoPago;
        pago.FechaPago = datos.FechaPago;
        pago.NumeroCuotas = pago.TipoPago == TipoPago.EXTRA ? 1 : 3;
        pago.FechaActualizacion = DateTime.Now;

        // Como ya se validó que no hay abonos, se puede borrar y regenerar
        // las cuotas sin perder ningún pago real del cliente.
        var cuotasViejas = await _context.Cuotas.Where(c => c.PagoId == pago.Id).ToListAsync();
        _context.Cuotas.RemoveRange(cuotasViejas);

        // Al corregir un pago observado, vuelve a PENDIENTE para que otro
        // abogado lo revise de nuevo; no se auto-confirma solo.
        if (eraObservado)
        {
            pago.Estado = EstadoPago.PENDIENTE;
            pago.UsuarioConfirmacionId = null;
            pago.FechaConfirmacion = null;
        }

        await _context.SaveChangesAsync();

        if (pago.TipoPago == TipoPago.EXTRA)
        {
            _context.Cuotas.Add(new Cuota
            {
                PagoId = pago.Id,
                NumeroCuota = 1,
                Monto = pago.MontoTotal,
                MontoPagado = 0,
                FechaVencimiento = pago.FechaPago,
                Estado = EstadoCuota.PENDIENTE,
                FormaPago = pago.FormaPago,
            });
        }
        else
        {
            GenerarCuotas(pago);
        }
        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Editó pago", "Pagos", pago.Id,
            $"Editó el pago \"{pago.Concepto}\" (S/ {pago.MontoTotal})");

        if (eraObservado)
        {
            await _notificaciones.NotificarAOtrosAsync(
                usuarioActual.Id,
                "Pago corregido, pendiente de confirmación",
                $"{usuarioActual.Nombres} corrigió el pago \"{pago.Concepto}\" que habías observado. Vuelve a revisarlo.",
                "PAGO_PENDIENTE",
                pago.Id);
        }

        TempData["Mensaje"] = "Pago actualizado correctamente.";
        return RedirectToAction(nameof(VerDetalle), new { id = pago.Id });
    }

    // POST /app/pagos/{id}/anular
    // Anular es la única forma de "borrar" un pago mal registrado; solo un
    // abogado puede hacerlo, y solo mientras el dinero no fue confirmado
    // como recibido.
    [HttpPost("{id:long}/anular")]
    [Authorize(Roles = PermisosUtil.RolAbogado)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(long id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Mensaje"] = "Debes indicar el motivo para anular el pago.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        var pago = await _context.Pagos.FindAsync(id);
        if (pago == null) return NotFound();

        if (pago.Estado == EstadoPago.CONFIRMADO)
        {
            TempData["Mensaje"] = "Este pago ya fue confirmado como dinero recibido y no se puede anular desde aquí.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        if (pago.Estado == EstadoPago.ANULADO)
        {
            TempData["Mensaje"] = "Este pago ya estaba anulado.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();

        pago.Estado = EstadoPago.ANULADO;
        pago.Observaciones = (string.IsNullOrEmpty(pago.Observaciones) ? "" : pago.Observaciones + " | ")
            + "Anulado: " + motivo;
        pago.FechaActualizacion = DateTime.Now;

        var cuotasDelPago = await _context.Cuotas.Where(c => c.PagoId == id).ToListAsync();
        foreach (var cuota in cuotasDelPago)
        {
            cuota.Estado = EstadoCuota.ANULADA;
        }

        await _context.SaveChangesAsync();

        await _registroActividad.RegistrarAsync(usuarioActual.Id, "Anuló pago", "Pagos", pago.Id,
            $"Anuló el pago \"{pago.Concepto}\" (S/ {pago.MontoTotal}) — Motivo: {motivo}");

        await _notificaciones.NotificarAsync(
            pago.UsuarioRegistroId,
            "Pago anulado",
            $"{usuarioActual.Nombres} anuló el pago \"{pago.Concepto}\". Motivo: {motivo}",
            "PAGO_ANULADO",
            pago.Id);

        TempData["Mensaje"] = "Pago anulado correctamente.";
        return RedirectToAction(nameof(VerDetalle), new { id });
    }

    // Un pago solo se puede editar si nadie le ha registrado abonos todavía
    // y no está confirmado ni ya anulado.
    private async Task<(bool ok, string motivo)> PuedeEditarseAsync(Pago pago)
    {
        if (pago.Estado == EstadoPago.CONFIRMADO)
            return (false, "Este pago ya fue confirmado como dinero recibido y no se puede editar. Si hay un error, anúlalo y registra uno nuevo.");
        if (pago.Estado == EstadoPago.ANULADO)
            return (false, "Este pago está anulado y no se puede editar.");

        var tieneAbonos = await _context.AbonosCuota.AnyAsync(a => a.Cuota!.PagoId == pago.Id);
        if (tieneAbonos)
            return (false, "Este pago ya tiene abonos registrados y no se puede editar sin afectar esos pagos. Anúlalo si fue un error.");

        return (true, string.Empty);
    }

    // POST /app/pagos/{id}/abono
    [HttpPost("{id:long}/abono")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarAbono(long id, decimal monto, DateTime? fecha, FormaPago? formaPago, string? observaciones)
    {
        if (monto <= 0)
        {
            TempData["Mensaje"] = "El monto del abono debe ser mayor a cero.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        var pagoDestino = await _context.Pagos.FindAsync(id);
        if (pagoDestino == null) return NotFound();

        if (pagoDestino.Estado == EstadoPago.ANULADO)
        {
            TempData["Mensaje"] = "Este pago está anulado, no se le pueden registrar abonos.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        var cuotasPago = await _context.Cuotas
            .Where(c => c.PagoId == id).OrderBy(c => c.NumeroCuota).ToListAsync();

        if (cuotasPago.Count == 0)
        {
            TempData["Mensaje"] = "Este pago no tiene cuotas registradas.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        var fechaAbono = fecha ?? DateTime.Today;

        // Solo se aplica a cuotas que todavia pueden recibir dinero
        // (no aplica a cuotas anuladas ni observadas).
        var cuotasConSaldo = cuotasPago
            .Where(c => c.Estado != EstadoCuota.ANULADA && c.Estado != EstadoCuota.OBSERVADA && c.SaldoPendiente > 0)
            .OrderBy(c => c.NumeroCuota)
            .ToList();

        if (cuotasConSaldo.Count == 0)
        {
            TempData["Mensaje"] = "Este pago ya no tiene cuotas pendientes de saldo.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        var saldoTotalPendiente = cuotasConSaldo.Sum(c => c.SaldoPendiente);
        if (monto > saldoTotalPendiente)
        {
            TempData["Mensaje"] = $"El abono (S/ {monto}) excede el saldo pendiente del pago (S/ {saldoTotalPendiente}).";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        var restante = monto;

        foreach (var cuota in cuotasConSaldo)
        {
            if (restante <= 0) break;

            var saldoCuota = cuota.SaldoPendiente;
            var aplicado = restante >= saldoCuota ? saldoCuota : restante;

            _context.AbonosCuota.Add(new AbonoCuota
            {
                CuotaId = cuota.Id,
                Monto = aplicado,
                FechaAbono = fechaAbono,
                FormaPago = formaPago ?? cuota.FormaPago,
                Observaciones = observaciones,
                UsuarioRegistroId = usuarioActual.Id,
                FechaRegistro = DateTime.Now,
            });

            cuota.MontoPagado += aplicado;
            cuota.Estado = cuota.MontoPagado >= cuota.Monto ? EstadoCuota.PAGADA : EstadoCuota.PARCIAL;
            if (cuota.Estado == EstadoCuota.PAGADA) cuota.FechaPago = fechaAbono;

            restante -= aplicado;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar abono sobre el pago {Id}", id);
            TempData["Mensaje"] = "No se pudo registrar el abono. Intenta nuevamente o contacta al administrador.";
            return RedirectToAction(nameof(VerDetalle), new { id });
        }

        var detalleCuotas = cuotasConSaldo.Count == 1
            ? $"cuota #{cuotasConSaldo[0].NumeroCuota}"
            : $"cuotas #{cuotasConSaldo[0].NumeroCuota} a #{cuotasConSaldo[^1].NumeroCuota}";
        await _registroActividad.RegistrarAsync(usuarioActual.Id, $"Registró abono de S/ {monto}", "Pagos", id,
            $"Registró un abono de S/ {monto} sobre la(s) {detalleCuotas}" +
                (!string.IsNullOrWhiteSpace(observaciones) ? $" — {observaciones}" : ""));

        TempData["Mensaje"] = "Abono registrado correctamente.";
        return RedirectToAction(nameof(VerDetalle), new { id });
    }

    // POST /app/pagos/cuotas/{cuotaId}/estado
    // Forzar el estado de una cuota a mano solo lo hace un ABOGADO.
    [HttpPost("cuotas/{cuotaId:long}/estado")]
    [Authorize(Roles = PermisosUtil.RolAbogado)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoCuota(long cuotaId, EstadoCuota estado, long pagoId)
    {
        // PAGADA y PARCIAL ya no se pueden forzar a mano aquí: antes esto
        // permitía marcar una cuota como PAGADA sin que hubiera ningún
        // abono real detrás, y luego RegistrarAbono() la volvía a cobrar
        // porque su MontoPagado seguía en 0. Ese camino ahora es siempre
        // "Registrar abono"; aquí solo se manejan los otros tres estados.
        if (estado != EstadoCuota.PENDIENTE && estado != EstadoCuota.OBSERVADA && estado != EstadoCuota.ANULADA)
        {
            TempData["Mensaje"] = "Para marcar una cuota como pagada, registra un abono por el monto correspondiente.";
            return RedirectToAction(nameof(VerDetalle), new { id = pagoId });
        }

        var cuota = await _context.Cuotas.FindAsync(cuotaId);
        if (cuota == null) return NotFound();

        cuota.Estado = estado;

        await _context.SaveChangesAsync();

        var usuarioActual = await _sesionUtil.ObtenerUsuarioActualAsync();
        await _registroActividad.RegistrarAsync(usuarioActual.Id, $"Actualizó cuota #{cuota.NumeroCuota} a {estado}", "Pagos", pagoId,
            $"Cuota #{cuota.NumeroCuota} marcada como {estado}");

        TempData["Mensaje"] = "Cuota actualizada correctamente.";
        return RedirectToAction(nameof(VerDetalle), new { id = pagoId });
    }

    private async Task CargarListasApoyoAsync()
    {
        ViewBag.Clientes = await _context.Clientes.Where(c => c.Estado).OrderBy(c => c.Apellidos).ToListAsync();
        ViewBag.Procesos = await _context.Procesos.Include(p => p.Cliente).ToListAsync();
        ViewBag.FormasPago = Enum.GetValues<FormaPago>();
    }
}
