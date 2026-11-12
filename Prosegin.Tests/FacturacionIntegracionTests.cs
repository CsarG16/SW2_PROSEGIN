using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.Services;
using Prosegin.Web.Services.Nubefact;
using Prosegin.Web.Services.Nubefact.DTOs;
using Prosegin.Web.ViewModels.Pedidos;
using Xunit;

namespace Prosegin.Tests;

public class FacturacionIntegracionTests
{
    [Fact]
    public async Task PrevisualizacionYEmision_CongelaDetalles_YRechazaUnaSegundaFactura()
    {
        await using var context = CrearContext();
        var cliente = new Cliente
        {
            Ruc = "20100070970",
            RazonSocial = "Cliente de prueba S.A.C.",
            DireccionFiscal = "Av. Fiscal 123",
            EstadoSunat = "ACTIVO",
            CondicionSunat = "HABIDO",
            Activo = true
        };
        var producto = new Producto
        {
            Sku = "EPP-001",
            Nombre = "Casco de seguridad",
            UnidadMedida = "UND",
            Activo = true
        };
        context.OrdenesVenta.Add(new OrdenVenta
        {
            EstadoLogistico = "EnRuta",
            Cotizacion = new Cotizacion
            {
                Correlativo = "COT-FACT-001",
                Estado = "Aprobada",
                NumeroOrdenCompraCliente = "OC-001",
                CondicionPago = "30 días",
                Cliente = cliente,
                Subtotal = 100m,
                Igv = 18m,
                Total = 118m,
                Detalles = new List<CotizacionDetalle>
                {
                    new()
                    {
                        Producto = producto,
                        NombreProducto = producto.Nombre,
                        Cantidad = 2,
                        PrecioVentaCalculado = 50m,
                        Subtotal = 100m
                    }
                }
            }
        });
        await context.SaveChangesAsync();

        var nubefact = new FakeNubefactService();
        var service = new PedidosService(
            context,
            nubefact,
            Options.Create(new NubefactSettings { SerieFactura = "FFF1" }));

        var preview = await service.PrevisualizarFacturaAsync("COT-FACT-001");
        Assert.NotNull(preview);
        Assert.Equal("20100070970", preview.ClienteRuc);
        Assert.Equal(100m, preview.Subtotal);
        Assert.Equal(18m, preview.Igv);
        Assert.Equal(118m, preview.Total);
        Assert.Equal("EPP-001", preview.Items.Single().Codigo);

        var emitted = await service.EmitirFacturaAsync("COT-FACT-001");

        Assert.True(emitted.Exito);
        Assert.Equal("Factura emitida con éxito", emitted.Mensaje);
        Assert.Equal("FFF1-1", emitted.NumeroFactura);
        Assert.Equal(1, nubefact.FacturasEnviadas);
        var factura = await context.Facturas.Include(f => f.Detalles).SingleAsync();
        Assert.Equal("Aceptado", factura.EstadoEmision);
        var ordenActualizada = await context.OrdenesVenta.SingleAsync(ov => ov.Cotizacion.Correlativo == "COT-FACT-001");
        Assert.Equal("Entregado", ordenActualizada.EstadoLogistico);
        var detalle = Assert.Single(factura.Detalles);
        Assert.Equal(50m, detalle.ValorUnitario);

        var second = await service.EmitirFacturaAsync("COT-FACT-001");
        Assert.False(second.Exito);
        Assert.Contains("en transporte", second.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, nubefact.FacturasEnviadas);
    }

    [Fact]
    public async Task Emision_RechazaClienteNoActivoOHabido()
    {
        await using var context = CrearContext();
        context.OrdenesVenta.Add(CrearOrden("INACTIVO", activo: false, condicion: "HABIDO"));
        await context.SaveChangesAsync();

        var nubefact = new FakeNubefactService();
        var service = new PedidosService(context, nubefact, Options.Create(new NubefactSettings()));

        var result = await service.EmitirFacturaAsync("INACTIVO");

        Assert.False(result.Exito);
        Assert.Contains("activo y habido", result.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, nubefact.FacturasEnviadas);
    }

    [Fact]
    public async Task FlujoListoDespacho_GuiaRemision_PasaAEnRuta_YFacturaPasaAEntregado()
    {
        await using var context = CrearContext();
        var cliente = new Cliente
        {
            Ruc = "20100070970",
            RazonSocial = "Cliente Flujo S.A.C.",
            DireccionFiscal = "Av. Fiscal 500",
            Ubigeo = "150101",
            EstadoSunat = "ACTIVO",
            CondicionSunat = "HABIDO",
            Activo = true
        };
        var producto = new Producto
        {
            Sku = "EPP-GUIA-01",
            Nombre = "Botas de seguridad",
            UnidadMedida = "PAR",
            PesoKg = 1.5m,
            Activo = true
        };
        var ordenVenta = new OrdenVenta
        {
            EstadoLogistico = "ListoParaDespacho",
            Cotizacion = new Cotizacion
            {
                Correlativo = "COT-FLUJO-001",
                Estado = "Aprobada",
                NumeroOrdenCompraCliente = "OC-FLUJO-001",
                CondicionPago = "15 días",
                Cliente = cliente,
                Subtotal = 200m,
                Igv = 36m,
                Total = 236m,
                Detalles = new List<CotizacionDetalle>
                {
                    new()
                    {
                        Producto = producto,
                        NombreProducto = producto.Nombre,
                        Cantidad = 2,
                        PrecioVentaCalculado = 100m,
                        Subtotal = 200m
                    }
                }
            }
        };
        context.OrdenesVenta.Add(ordenVenta);
        await context.SaveChangesAsync();

        var nubefact = new FakeNubefactService();
        var service = new PedidosService(
            context,
            nubefact,
            Options.Create(new NubefactSettings { SerieFactura = "FFF1", SerieGuia = "TTT1" }));

        // 1. En ListoParaDespacho, no se puede emitir la factura todavía
        var facturaRechazada = await service.EmitirFacturaAsync("COT-FLUJO-001");
        Assert.False(facturaRechazada.Exito);
        Assert.Contains("Solo se puede facturar un pedido en transporte", facturaRechazada.Mensaje);

        // 2. Se emite la guía de remisión y pasa automáticamente a EnRuta (En transporte)
        var guiaResult = await service.EmitirGuiaRemisionAsync(new EmitirGuiaRemisionRequest
        {
            NumeroPedido = "COT-FLUJO-001",
            ConductorNombre = "Juan Perez",
            PlacaVehiculo = "ABC-123"
        });
        Assert.True(guiaResult.Exito);
        Assert.Equal(1, nubefact.GuiasEnviadas);

        var ordenActualizada = await context.OrdenesVenta.SingleAsync(ov => ov.Cotizacion.Correlativo == "COT-FLUJO-001");
        Assert.Equal("EnRuta", ordenActualizada.EstadoLogistico);

        var detalleEnRuta = await service.ObtenerDetallePedidoAsync("COT-FLUJO-001");
        Assert.NotNull(detalleEnRuta);
        Assert.True(detalleEnRuta.PuedeGenerarFactura);

        // 3. Estando en transporte, se puede previsualizar y emitir la factura y pasa automáticamente a Entregado
        var preview = await service.PrevisualizarFacturaAsync("COT-FLUJO-001");
        Assert.NotNull(preview);

        var facturaResult = await service.EmitirFacturaAsync("COT-FLUJO-001");
        Assert.True(facturaResult.Exito);
        Assert.Equal(1, nubefact.FacturasEnviadas);
        Assert.Equal("Entregado", ordenActualizada.EstadoLogistico);

        var detalleFinal = await service.ObtenerDetallePedidoAsync("COT-FLUJO-001");
        Assert.NotNull(detalleFinal);
        Assert.Equal("ENTREGADO", detalleFinal.EstadoOperativo);
        Assert.Null(detalleFinal.BloqueoOperativo);
        Assert.False(detalleFinal.PuedeGenerarFactura);
        Assert.False(detalleFinal.PuedeGenerarGuiaRemision);
        Assert.False(detalleFinal.PuedeCancelar);
    }

    private static OrdenVenta CrearOrden(string correlativo, bool activo, string condicion)
    {
        var cliente = new Cliente
        {
            Ruc = "20492817263",
            RazonSocial = correlativo,
            DireccionFiscal = "Dirección",
            Activo = activo,
            EstadoSunat = "ACTIVO",
            CondicionSunat = condicion
        };
        var producto = new Producto { Sku = correlativo, Nombre = "Producto", UnidadMedida = "UND" };
        return new OrdenVenta
        {
            EstadoLogistico = "EnRuta",
            Cotizacion = new Cotizacion
            {
                Correlativo = correlativo,
                Estado = "Aprobada",
                CondicionPago = "7 días",
                Cliente = cliente,
                Subtotal = 10m,
                Igv = 1.8m,
                Total = 11.8m,
                Detalles = new List<CotizacionDetalle>
                {
                    new() { Producto = producto, NombreProducto = "Producto", Cantidad = 1, PrecioVentaCalculado = 10m, Subtotal = 10m }
                }
            }
        };
    }

    private static ProseginDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<ProseginDbContext>()
            .UseSqlite($"Data Source=file:facturacion-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;
        var context = new ProseginDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class FakeNubefactService : INubefactService
    {
        public int FacturasEnviadas { get; private set; }
        public int GuiasEnviadas { get; private set; }

        public Task<NubefactResponseDto> EnviarFacturaAsync(NubefactFacturaRequest request, CancellationToken ct = default)
        {
            FacturasEnviadas++;
            return Task.FromResult(new NubefactResponseDto
            {
                EstadoProceso = NubefactService.EstadoAceptado,
                Serie = request.Serie,
                Numero = request.Numero,
                CodigoHash = "hash-prueba",
                EnlacePdf = "https://example.test/factura.pdf"
            });
        }

        public Task<NubefactResponseDto> EnviarGuiaRemisionAsync(NubefactGuiaRequest request, CancellationToken ct = default)
        {
            GuiasEnviadas++;
            return Task.FromResult(new NubefactResponseDto
            {
                EstadoProceso = NubefactService.EstadoAceptado,
                Serie = request.Serie,
                Numero = request.Numero,
                CodigoHash = "hash-guia",
                EnlacePdf = "https://example.test/guia.pdf"
            });
        }

        public Task<NubefactResponseDto> ConsultarGuiaRemisionAsync(string serie, int numero, CancellationToken ct = default)
        {
            return Task.FromResult(new NubefactResponseDto
            {
                EstadoProceso = NubefactService.EstadoAceptado,
                Serie = serie,
                Numero = numero,
                CodigoHash = "hash-guia-consultada",
                EnlacePdf = $"https://example.test/{serie}-{numero}.pdf",
                EnlaceXml = $"https://example.test/{serie}-{numero}.xml",
                EnlaceCdr = $"https://example.test/{serie}-{numero}.cdr"
            });
        }
    }
}
