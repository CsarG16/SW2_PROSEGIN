using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.Services.Nubefact;

namespace Prosegin.Web.Services;

/// <summary>
/// Datos de prueba para la HU de facturas por vencer: 3 facturas pendientes, una por cada tramo
/// (0-7, 8-15 y 16-30 días). Es idempotente: no vuelve a crearlas si ya existen.
/// Debe ejecutarse después de ClienteDataSeeder y DbSeeder (necesita clientes y productos).
/// </summary>
public static class FacturaDataSeeder
{
    private const decimal Igv = 0.18m;

    private sealed record Muestra(string Numero, int DiasParaVencer, int PlazoCredito, int Cantidad, decimal PrecioConIgv);

    private static readonly Muestra[] Muestras =
    [
        new("F001-9001", 3, 30, 12, 59.00m),    // tramo 0-7 días
        new("F001-9002", 10, 30, 20, 35.50m),   // tramo 8-15 días
        new("F001-9003", 20, 45, 8, 118.00m)    // tramo 16-30 días
    ];

    public static async Task SeedAsync(ProseginDbContext context)
    {
        var yaCreadas = await context.Facturas
            .Where(f => Muestras.Select(m => m.Numero).Contains(f.NumeroFactura))
            .Select(f => f.NumeroFactura)
            .ToListAsync();
        if (yaCreadas.Count == Muestras.Length)
            return;

        var clientes = await context.Clientes.Where(c => c.Activo).OrderBy(c => c.Id).Take(3).ToListAsync();
        var productos = await context.Productos.Where(p => p.Activo).OrderBy(p => p.Id).Take(3).ToListAsync();
        if (clientes.Count == 0 || productos.Count == 0)
            return;

        var hoy = Prosegin.Web.HoraPeru.Hoy;
        for (var i = 0; i < Muestras.Length; i++)
        {
            var m = Muestras[i];
            if (yaCreadas.Contains(m.Numero))
                continue;

            var cliente = clientes[i % clientes.Count];
            var producto = productos[i % productos.Count];
            var vencimiento = hoy.AddDays(m.DiasParaVencer);
            var emision = vencimiento.AddDays(-m.PlazoCredito);
            var total = Math.Round(m.Cantidad * m.PrecioConIgv, 2);
            var subtotal = Math.Round(total / (1 + Igv), 2);
            var igv = total - subtotal;
            var valorUnitario = Math.Round(m.PrecioConIgv / (1 + Igv), 2);

            var cotizacion = new Cotizacion
            {
                Correlativo = $"COT-SEED-{9001 + i}",
                ClienteId = cliente.Id,
                FechaEmision = emision.AddHours(14),
                FechaVencimiento = emision.AddDays(15),
                Estado = "Aprobada",
                CondicionPago = $"Crédito {m.PlazoCredito} días",
                NumeroOrdenCompraCliente = $"OC-SEED-{9001 + i}",
                Subtotal = subtotal,
                Igv = igv,
                Total = total,
                Detalles =
                {
                    new CotizacionDetalle
                    {
                        ProductoId = producto.Id,
                        NombreProducto = producto.Nombre,
                        Cantidad = m.Cantidad,
                        CostoProveedorReferencial = producto.CostoReferencial,
                        MargenDeseado = 0.30m,
                        PrecioVentaCalculado = valorUnitario,
                        Subtotal = subtotal
                    }
                }
            };

            var orden = new OrdenVenta
            {
                Cotizacion = cotizacion,
                FechaCreacion = emision.AddHours(15),
                EstadoLogistico = "Entregado"
            };

            var factura = new Factura
            {
                OrdenVenta = orden,
                ClienteId = cliente.Id,
                NumeroFactura = m.Numero,
                Serie = "F001",
                Numero = 9001 + i,
                FechaEmision = emision.AddHours(20),
                FechaVencimiento = vencimiento,
                EstadoPago = "Pendiente",
                EstadoEmision = NubefactService.EstadoAceptado,
                Total = total,
                Detalles =
                {
                    new FacturaDetalle
                    {
                        Codigo = producto.Sku,
                        Descripcion = producto.Nombre,
                        UnidadMedida = "NIU",
                        Cantidad = m.Cantidad,
                        ValorUnitario = valorUnitario,
                        PrecioUnitario = m.PrecioConIgv,
                        Subtotal = subtotal,
                        Igv = igv,
                        Total = total
                    }
                }
            };

            context.Facturas.Add(factura);
        }

        await context.SaveChangesAsync();
    }
}
