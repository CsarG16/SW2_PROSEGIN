using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Data.Validation;
using Prosegin.Web.ViewModels.Pedidos;

namespace Prosegin.Web.Controllers;

public class AbastecimientoController : Controller
{
    private readonly ProseginDbContext _context;

    public AbastecimientoController(ProseginDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int id, CancellationToken cancellationToken)
    {
        var ordenVenta = await ObtenerOrdenVentaAsync(id, cancellationToken);
        if (ordenVenta == null)
        {
            return NotFound();
        }

        if (!EsPedidoPorPreparar(ordenVenta) || ordenVenta.OrdenesCompra.Count > 0)
        {
            TempData["ModalError"] = "El pedido ya no está disponible para generar órdenes de compra.";
            return RedirectToAction("Index", "Pedidos");
        }

        var model = CrearModelo(ordenVenta);
        if (model.Productos.Count == 0)
        {
            model.Error = "El pedido confirmado no contiene productos para abastecer.";
        }
        else if (model.Productos.Any(p => p.Proveedores.Count == 0))
        {
            model.Error = "Hay productos sin proveedores autorizados y con tarifa configurada en el catálogo.";
        }

        ViewData["Title"] = $"Abastecimiento de Pedido: {model.NumeroPedido}";
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerarOrdenesCompra(
        int ordenVentaId,
        List<OrdenProveedorSeleccionViewModel> selecciones,
        CancellationToken cancellationToken)
    {
        var ordenVenta = await ObtenerOrdenVentaAsync(ordenVentaId, cancellationToken);
        if (ordenVenta == null)
        {
            return NotFound();
        }

        if (!EsPedidoPorPreparar(ordenVenta) || ordenVenta.OrdenesCompra.Count > 0)
        {
            TempData["ModalError"] = "El pedido ya no está disponible para generar órdenes de compra.";
            return RedirectToAction(nameof(Index), new { id = ordenVentaId });
        }

        var detalles = ordenVenta.Cotizacion.Detalles.OrderBy(d => d.Id).ToList();
        if (detalles.Count == 0
            || selecciones.Count != detalles.Count
            || selecciones.Select(s => s.ProductoId).Distinct().Count() != detalles.Count
            || !detalles.Select(d => d.ProductoId).OrderBy(id => id)
                .SequenceEqual(selecciones.Select(s => s.ProductoId).OrderBy(id => id)))
        {
            TempData["ModalError"] = "No se pudo validar la selección de proveedores para todos los productos.";
            return RedirectToAction(nameof(Index), new { id = ordenVentaId });
        }

        var costosPorDetalle = new Dictionary<int, (ProductoProveedor Tarifa, CotizacionDetalle Detalle)>();
        foreach (var detalle in detalles)
        {
            var seleccion = selecciones.Single(s => s.ProductoId == detalle.ProductoId);
            var tarifa = detalle.Producto.ProveedoresAutorizados
                .SingleOrDefault(p => p.ProveedorId == seleccion.ProveedorId);
            if (tarifa == null || tarifa.CostoCompra <= 0)
            {
                TempData["ModalError"] = $"El proveedor seleccionado ya no está autorizado o no tiene tarifa para {detalle.Producto.Nombre}.";
                return RedirectToAction(nameof(Index), new { id = ordenVentaId });
            }

            costosPorDetalle.Add(detalle.Id, (tarifa, detalle));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        foreach (var grupo in costosPorDetalle.Values.GroupBy(item => item.Tarifa.ProveedorId))
        {
            var lineas = grupo.Select(item =>
            {
                var subtotal = decimal.Round(
                    item.Detalle.Cantidad * item.Tarifa.CostoCompra,
                    2,
                    MidpointRounding.AwayFromZero);
                return new OrdenCompraDetalle
                {
                    ProductoId = item.Detalle.ProductoId,
                    Cantidad = item.Detalle.Cantidad,
                    CostoUnitario = item.Tarifa.CostoCompra,
                    Subtotal = subtotal
                };
            }).ToList();
            _context.OrdenesCompra.Add(new OrdenCompra
            {
                ProveedorId = grupo.Key,
                OrdenVentaId = ordenVenta.Id,
                FechaCompra = DateTime.UtcNow,
                Estado = "Pendiente",
                Total = decimal.Round(lineas.Sum(linea => linea.Subtotal), 2, MidpointRounding.AwayFromZero),
                Detalles = lineas
            });
        }

        ordenVenta.EstadoLogistico = "EnPreparacion";
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["ModalSuccess"] = "Órdenes de compra generadas y consolidadas con éxito";
        return RedirectToAction("Index", "Pedidos", new { estado = PedidoLogisticaRules.EstadoEnPreparacion });
    }

    private Task<OrdenVenta?> ObtenerOrdenVentaAsync(int id, CancellationToken cancellationToken)
    {
        return _context.OrdenesVenta
            .Include(ov => ov.Cotizacion)
                .ThenInclude(c => c.Cliente)
            .Include(ov => ov.Cotizacion)
                .ThenInclude(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.ProveedoresAutorizados)
                            .ThenInclude(pp => pp.Proveedor)
            .Include(ov => ov.OrdenesCompra)
            .FirstOrDefaultAsync(ov => ov.Id == id, cancellationToken);
    }

    private static bool EsPedidoPorPreparar(OrdenVenta ordenVenta)
    {
        return ordenVenta.Cotizacion.Estado == "Aprobada"
            && !string.IsNullOrWhiteSpace(ordenVenta.Cotizacion.NumeroOrdenCompraCliente)
            && ordenVenta.EstadoLogistico.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Equals("PorPreparar", StringComparison.OrdinalIgnoreCase);
    }

    private static AbastecimientoViewModel CrearModelo(OrdenVenta ordenVenta)
    {
        var productos = ordenVenta.Cotizacion.Detalles
            .OrderBy(d => d.Id)
            .Select(detalle => new AbastecimientoProductoViewModel
            {
                ProductoId = detalle.ProductoId,
                Sku = detalle.Producto.Sku,
                Nombre = detalle.Producto.Nombre,
                Cantidad = detalle.Cantidad,
                UnidadMedida = detalle.Producto.UnidadMedida,
                Proveedores = detalle.Producto.ProveedoresAutorizados
                    .Where(pp => pp.CostoCompra > 0)
                    .OrderBy(pp => pp.Proveedor.RazonSocial)
                    .Select(pp => new AbastecimientoProveedorOpcionViewModel
                    {
                        Id = pp.ProveedorId,
                        RazonSocial = pp.Proveedor.RazonSocial,
                        CostoUnitario = decimal.Round(pp.CostoCompra, 2, MidpointRounding.AwayFromZero),
                        PlazoEntregaHoras = pp.PlazoEntregaHoras,
                        EsPrincipal = pp.EsPrincipal
                    })
                    .ToList()
            })
            .ToList();

        SugerirProveedores(productos);
        return new AbastecimientoViewModel
        {
            OrdenVentaId = ordenVenta.Id,
            NumeroPedido = ordenVenta.Cotizacion.Correlativo,
            NumeroOrdenCompraCliente = ordenVenta.Cotizacion.NumeroOrdenCompraCliente!,
            Cliente = ordenVenta.Cotizacion.Cliente.RazonSocial,
            DireccionEntrega = ordenVenta.Cotizacion.Cliente.DireccionFiscal,
            Productos = productos
        };
    }

    private static void SugerirProveedores(IReadOnlyList<AbastecimientoProductoViewModel> productos)
    {
        var productosConProveedor = productos
            .Where(producto => producto.Proveedores.Count > 0)
            .ToList();
        if (productosConProveedor.Count == 0)
        {
            return;
        }

        var proveedores = productosConProveedor
            .SelectMany(producto => producto.Proveedores)
            .Select(proveedor => proveedor.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var costoMinimoPosible = decimal.Round(
            productosConProveedor.Sum(producto => producto.Cantidad
                * producto.Proveedores.Min(proveedor => proveedor.CostoUnitario)),
            2,
            MidpointRounding.AwayFromZero);
        var mejorPlan = EvaluarAsignacion(productosConProveedor, proveedores, costoMinimoPosible);

        if (proveedores.Count <= 12)
        {
            var combinaciones = 1 << proveedores.Count;
            for (var mascara = 1; mascara < combinaciones; mascara++)
            {
                var permitidos = proveedores
                    .Where((_, indice) => (mascara & (1 << indice)) != 0)
                    .ToList();
                var plan = EvaluarAsignacion(productosConProveedor, permitidos, costoMinimoPosible);
                if (plan != null && (mejorPlan == null || CompararPlanes(plan, mejorPlan) < 0))
                {
                    mejorPlan = plan;
                }
            }
        }
        else
        {
            var proveedoresActivos = mejorPlan!.Asignaciones.Select(a => a.Proveedor.Id).Distinct().ToList();
            var continuar = true;
            while (continuar && proveedoresActivos.Count > 1)
            {
                continuar = false;
                var candidatos = new List<PlanAbastecimiento>();
                foreach (var proveedorId in proveedoresActivos)
                {
                    var permitidos = proveedoresActivos.Where(id => id != proveedorId).ToList();
                    var candidato = EvaluarAsignacion(productosConProveedor, permitidos, costoMinimoPosible);
                    if (candidato != null && CompararPlanes(candidato, mejorPlan!) < 0)
                    {
                        candidatos.Add(candidato);
                    }
                }

                if (candidatos.Count > 0)
                {
                    mejorPlan = candidatos.OrderBy(p => p.Puntaje)
                        .ThenBy(p => p.CostoTotal)
                        .ThenBy(p => p.CantidadProveedores)
                        .First();
                    proveedoresActivos = mejorPlan.Asignaciones.Select(a => a.Proveedor.Id).Distinct().ToList();
                    continuar = true;
                }
            }
        }

        if (mejorPlan == null)
        {
            return;
        }

        foreach (var asignacion in mejorPlan.Asignaciones)
        {
            asignacion.Producto.ProveedorSeleccionadoId = asignacion.Proveedor.Id;
        }
    }

    private static PlanAbastecimiento? EvaluarAsignacion(
        IReadOnlyList<AbastecimientoProductoViewModel> productos,
        IReadOnlyCollection<int> proveedoresPermitidos,
        decimal costoMinimoPosible)
    {
        var asignaciones = new List<AsignacionAbastecimiento>();
        foreach (var producto in productos)
        {
            var proveedor = producto.Proveedores
                .Where(opcion => proveedoresPermitidos.Contains(opcion.Id))
                .OrderBy(opcion => opcion.CostoUnitario)
                .ThenByDescending(opcion => opcion.EsPrincipal)
                .ThenBy(opcion => opcion.RazonSocial, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (proveedor == null)
            {
                return null;
            }

            asignaciones.Add(new AsignacionAbastecimiento(producto, proveedor));
        }

        var costoTotal = decimal.Round(
            asignaciones.Sum(asignacion => asignacion.Producto.Cantidad * asignacion.Proveedor.CostoUnitario),
            2,
            MidpointRounding.AwayFromZero);
        var cantidadProveedores = asignaciones.Select(asignacion => asignacion.Proveedor.Id).Distinct().Count();
        return new PlanAbastecimiento(
            asignaciones,
            costoTotal,
            cantidadProveedores,
            costoTotal / costoMinimoPosible + (decimal)cantidadProveedores / productos.Count);
    }

    private static int CompararPlanes(PlanAbastecimiento left, PlanAbastecimiento right)
    {
        var puntaje = left.Puntaje.CompareTo(right.Puntaje);
        if (puntaje != 0)
        {
            return puntaje;
        }

        var costo = left.CostoTotal.CompareTo(right.CostoTotal);
        if (costo != 0)
        {
            return costo;
        }

        var cantidad = left.CantidadProveedores.CompareTo(right.CantidadProveedores);
        if (cantidad != 0)
        {
            return cantidad;
        }

        return string.Compare(
            string.Join(",", left.Asignaciones.Select(a => a.Proveedor.Id)),
            string.Join(",", right.Asignaciones.Select(a => a.Proveedor.Id)),
            StringComparison.Ordinal);
    }

    private sealed record AsignacionAbastecimiento(
        AbastecimientoProductoViewModel Producto,
        AbastecimientoProveedorOpcionViewModel Proveedor);

    private sealed record PlanAbastecimiento(
        List<AsignacionAbastecimiento> Asignaciones,
        decimal CostoTotal,
        int CantidadProveedores,
        decimal Puntaje);
}
