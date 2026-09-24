using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.Services;
using Prosegin.Web.ViewModels.Cotizaciones;

namespace Prosegin.Web.Controllers;

public class CotizacionesController : Controller
{
    private readonly ProseginDbContext _context;
    private readonly ISunatService _sunatService;

    public CotizacionesController(ProseginDbContext context, ISunatService sunatService)
    {
        _context = context;
        _sunatService = sunatService;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int clienteId, CancellationToken cancellationToken)
    {
        var model = await BuildModelAsync(clienteId, cancellationToken);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CotizacionClienteViewModel model, CancellationToken cancellationToken)
    {
        var current = await BuildModelAsync(model.ClienteId, cancellationToken);
        if (current == null)
        {
            return NotFound();
        }

        model.Ruc = current.Ruc;
        model.RazonSocial = current.RazonSocial;
        model.DireccionFiscal = current.DireccionFiscal;
        model.RepresentanteLegal = current.RepresentanteLegal;
        model.EstadoSunat = current.EstadoSunat;
        model.CondicionSunat = current.CondicionSunat;
        model.LimiteCredito = current.LimiteCredito;
        model.SaldoPendiente = current.SaldoPendiente;
        model.SaldoVencido = current.SaldoVencido;
        model.DiasMora = current.DiasMora;
        model.ClienteBloqueado = current.ClienteBloqueado;
        model.MotivoBloqueo = current.MotivoBloqueo;

        var noPuedeVender = !string.Equals(model.EstadoSunat, "ACTIVO", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(model.CondicionSunat, "HABIDO", StringComparison.OrdinalIgnoreCase);
        var solicitaCredito = !string.Equals(model.CondicionPago, "Contado", StringComparison.OrdinalIgnoreCase);

        if (noPuedeVender)
        {
            ModelState.AddModelError(string.Empty, "No se puede generar la cotización: el cliente no figura ACTIVO y HABIDO en SUNAT.");
        }
        else if (current.DatosSunatDesactualizados)
        {
            ModelState.AddModelError(string.Empty, "Debe actualizar los datos fiscales del cliente según SUNAT antes de generar el documento de venta.");
        }
        else if (solicitaCredito && model.ClienteBloqueado)
        {
            ModelState.AddModelError(string.Empty, $"Cliente Bloqueado por Mora: no se permiten cotizaciones a crédito. {model.MotivoBloqueo}");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var cotizacion = new Cotizacion
        {
            ClienteId = model.ClienteId,
            Correlativo = $"COT-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CondicionPago = model.CondicionPago,
            Subtotal = Math.Round(model.Total / 1.18m, 2),
            Igv = Math.Round(model.Total - (model.Total / 1.18m), 2),
            Total = model.Total,
            Estado = "Borrador"
        };

        _context.Cotizaciones.Add(cotizacion);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Cotización {cotizacion.Correlativo} creada correctamente.";
        return RedirectToAction(nameof(Create), new { clienteId = model.ClienteId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarDesdeSunat(int clienteId, CancellationToken cancellationToken)
    {
        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId && c.Activo, cancellationToken);
        if (cliente == null)
        {
            return NotFound();
        }

        var resultado = await _sunatService.ConsultarRucAsync(cliente.Ruc, cancellationToken);
        if (!resultado.Success)
        {
            TempData["ErrorMessage"] = resultado.Message;
            return RedirectToAction(nameof(Create), new { clienteId });
        }

        cliente.RazonSocial = resultado.RazonSocial?.Trim().ToUpper() ?? cliente.RazonSocial;
        cliente.DireccionFiscal = resultado.DireccionFiscal?.Trim() ?? cliente.DireccionFiscal;
        cliente.Departamento = resultado.Departamento?.Trim();
        cliente.Provincia = resultado.Provincia?.Trim();
        cliente.Distrito = resultado.Distrito?.Trim();
        cliente.Ubigeo = resultado.Ubigeo?.Trim();
        cliente.EstadoSunat = resultado.Estado?.Trim().ToUpper() ?? cliente.EstadoSunat;
        cliente.CondicionSunat = resultado.Condicion?.Trim().ToUpper() ?? cliente.CondicionSunat;
        cliente.RepresentanteLegal = string.IsNullOrWhiteSpace(resultado.RepresentanteLegal)
            ? cliente.RepresentanteLegal
            : resultado.RepresentanteLegal.Trim().ToUpper();
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Los datos fiscales fueron actualizados con la información más reciente de SUNAT.";
        return RedirectToAction(nameof(Create), new { clienteId });
    }

    private async Task<CotizacionClienteViewModel?> BuildModelAsync(int clienteId, CancellationToken cancellationToken)
    {
        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteId && c.Activo, cancellationToken);
        if (cliente == null)
        {
            return null;
        }

        var facturas = await _context.Facturas
            .AsNoTracking()
            .Where(f => f.ClienteId == clienteId && f.EstadoPago != "Pagado")
            .ToListAsync(cancellationToken);
        var ahora = DateTime.UtcNow;
        var vencidas = facturas.Where(f => f.FechaVencimiento < ahora).ToList();
        var saldoPendiente = facturas.Sum(f => f.Total);
        var saldoVencido = vencidas.Sum(f => f.Total);
        var diasMora = vencidas.Count == 0
            ? 0
            : vencidas.Max(f => Math.Max(0, (ahora.Date - f.FechaVencimiento.Date).Days));
        var sobregiro = cliente.LimiteCredito > 0 && saldoPendiente > cliente.LimiteCredito;
        var bloqueado = vencidas.Count > 0 || sobregiro;
        var resultadoSunat = await _sunatService.ConsultarRucAsync(cliente.Ruc, cancellationToken);
        var datosSunatDesactualizados = resultadoSunat.Success
            && ((!string.IsNullOrWhiteSpace(resultadoSunat.DireccionFiscal)
                && !string.Equals(cliente.DireccionFiscal.Trim(), resultadoSunat.DireccionFiscal.Trim(), StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(resultadoSunat.RepresentanteLegal)
                    && !string.Equals(cliente.RepresentanteLegal?.Trim(), resultadoSunat.RepresentanteLegal.Trim(), StringComparison.OrdinalIgnoreCase)));
        var estadoSunat = resultadoSunat.Success ? resultadoSunat.Estado ?? cliente.EstadoSunat : cliente.EstadoSunat;
        var condicionSunat = resultadoSunat.Success ? resultadoSunat.Condicion ?? cliente.CondicionSunat : cliente.CondicionSunat;

        return new CotizacionClienteViewModel
        {
            ClienteId = cliente.Id,
            Ruc = cliente.Ruc,
            RazonSocial = cliente.RazonSocial,
            DireccionFiscal = cliente.DireccionFiscal,
            RepresentanteLegal = cliente.RepresentanteLegal,
            EstadoSunat = estadoSunat,
            CondicionSunat = condicionSunat,
            LimiteCredito = cliente.LimiteCredito,
            SaldoPendiente = saldoPendiente,
            SaldoVencido = saldoVencido,
            DiasMora = diasMora,
            ClienteBloqueado = bloqueado,
            DatosSunatDesactualizados = datosSunatDesactualizados,
            MotivoBloqueo = vencidas.Count > 0
                ? $"Tiene facturas vencidas hasta {diasMora} días."
                : sobregiro
                    ? "Ha sobrepasado su línea de crédito aprobada."
                    : null
        };
    }
}
