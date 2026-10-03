using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Data.Validation;
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

        var productosEnviados = model.ProductosDisponibles;
        foreach (var producto in current.ProductosDisponibles)
        {
            var enviado = productosEnviados.FirstOrDefault(p => p.ProductoId == producto.ProductoId);
            if (enviado == null)
            {
                continue;
            }

            producto.Seleccionado = enviado.Seleccionado;
            producto.Cantidad = enviado.Cantidad;
            producto.MargenPorcentaje = enviado.MargenPorcentaje;
        }

        if (!CondicionPagoRules.EsPlazoCreditoValido(model.CondicionPago))
        {
            ModelState.AddModelError(nameof(model.CondicionPago), "Seleccione un plazo de crédito válido: 7, 15 o 30 días.");
            current.CondicionPago = CondicionPagoRules.PlazoCreditoPredeterminado;
        }
        else
        {
            current.CondicionPago = model.CondicionPago.Trim();
        }

        var noPuedeVender = !ClienteBusquedaRules.PuedeAbrirCotizacion(current.EstadoSunat, current.CondicionSunat);

        if (noPuedeVender)
        {
            ModelState.AddModelError(string.Empty, "No se puede generar la cotización: el cliente no figura ACTIVO y HABIDO en SUNAT.");
        }
        else if (current.DatosSunatDesactualizados)
        {
            ModelState.AddModelError(string.Empty, "Debe actualizar los datos fiscales del cliente según SUNAT antes de generar el documento de venta.");
        }
        else if (current.ClienteBloqueado)
        {
            ModelState.AddModelError(string.Empty, $"Cliente Bloqueado por Mora: no se permiten cotizaciones a crédito. {current.MotivoBloqueo}");
        }

        var productosSeleccionados = productosEnviados.Where(p => p.Seleccionado).ToList();
        if (productosSeleccionados.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Selecciona al menos un producto del catálogo.");
        }

        var idsSeleccionados = productosSeleccionados.Select(p => p.ProductoId).ToList();
        if (idsSeleccionados.Count != idsSeleccionados.Distinct().Count())
        {
            ModelState.AddModelError(string.Empty, "No se puede agregar el mismo producto más de una vez.");
        }

        var productosActivos = await _context.Productos
            .Where(p => p.Activo && idsSeleccionados.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (productosActivos.Count != idsSeleccionados.Distinct().Count())
        {
            ModelState.AddModelError(string.Empty, "Uno o más productos seleccionados ya no están disponibles en el catálogo.");
        }

        var detalles = new List<CotizacionDetalle>();
        foreach (var seleccionado in productosSeleccionados)
        {
            if (!productosActivos.TryGetValue(seleccionado.ProductoId, out var producto)
                || seleccionado.Cantidad < 1
                || seleccionado.MargenPorcentaje is < 0 or > 100)
            {
                continue;
            }

            var precioVenta = Math.Round(producto.CostoReferencial * (1 + seleccionado.MargenPorcentaje / 100m), 2);
            detalles.Add(new CotizacionDetalle
            {
                ProductoId = producto.Id,
                Cantidad = seleccionado.Cantidad,
                CostoProveedorReferencial = producto.CostoReferencial,
                MargenDeseado = seleccionado.MargenPorcentaje / 100m,
                PrecioVentaCalculado = precioVenta,
                Subtotal = Math.Round(precioVenta * seleccionado.Cantidad, 2)
            });
        }

        var subtotal = detalles.Sum(d => d.Subtotal);
        var igv = Math.Round(subtotal * 0.18m, 2);
        current.Total = subtotal + igv;

        if (!ModelState.IsValid)
        {
            return View(current);
        }

        var cotizacion = new Cotizacion
        {
            ClienteId = current.ClienteId,
            Correlativo = $"COT-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CondicionPago = current.CondicionPago,
            Subtotal = subtotal,
            Igv = igv,
            Total = current.Total,
            Estado = "Borrador",
            Detalles = detalles
        };

        _context.Cotizaciones.Add(cotizacion);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Productos guardados correctamente";
        return RedirectToAction(nameof(Create), new { clienteId = current.ClienteId });
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
        var bloqueado = vencidas.Count > 0;
        var resultadoSunat = await _sunatService.ConsultarRucAsync(cliente.Ruc, cancellationToken);
        var datosSunatDesactualizados = resultadoSunat.Success
            && (!string.IsNullOrWhiteSpace(resultadoSunat.DireccionFiscal)
                && !string.Equals(cliente.DireccionFiscal.Trim(), resultadoSunat.DireccionFiscal.Trim(), StringComparison.OrdinalIgnoreCase));
        var estadoSunat = resultadoSunat.Success ? resultadoSunat.Estado ?? cliente.EstadoSunat : cliente.EstadoSunat;
        var condicionSunat = resultadoSunat.Success ? resultadoSunat.Condicion ?? cliente.CondicionSunat : cliente.CondicionSunat;

        var model = new CotizacionClienteViewModel
        {
            ClienteId = cliente.Id,
            Ruc = cliente.Ruc,
            RazonSocial = cliente.RazonSocial,
            DireccionFiscal = cliente.DireccionFiscal,
            EstadoSunat = estadoSunat,
            CondicionSunat = condicionSunat,
            SaldoPendiente = saldoPendiente,
            SaldoVencido = saldoVencido,
            DiasMora = diasMora,
            ClienteBloqueado = bloqueado,
            DatosSunatDesactualizados = datosSunatDesactualizados,
            MotivoBloqueo = vencidas.Count > 0
                ? $"Tiene facturas vencidas hasta {diasMora} días."
                : null
        };

        model.ProductosDisponibles = await _context.Productos
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new ProductoCotizacionViewModel
            {
                ProductoId = p.Id,
                Sku = p.Sku,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                UnidadMedida = p.UnidadMedida,
                CostoReferencial = p.CostoReferencial,
                StockDisponible = p.StockDisponible,
                RutaImagen = p.RutaImagen ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return model;
    }
}
