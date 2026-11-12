using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.Controllers;
using Xunit;

namespace Prosegin.Tests;

public sealed class FinanzasIntegracionTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"prosegin-finanzas-{Guid.NewGuid():N}.db");
    private int _clienteUnoId;
    private int _clienteDosId;

    public async Task InitializeAsync()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Prosegin.slnx")))
            root = root.Parent;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FinanzasController).Assembly.GetName().Name,
            ContentRootPath = Path.Combine(root!.FullName, "Prosegin.Web"),
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(FinanzasController).Assembly);
        builder.Services.AddDbContext<ProseginDbContext>(options => options.UseSqlite($"Data Source={_database};Pooling=False"));
        builder.Services.AddHttpClient("ComprobantesClient")
            .ConfigurePrimaryHttpMessageHandler(() => new PdfResponseHandler());

        _app = builder.Build();
        _app.UseStaticFiles();
        _app.MapControllerRoute("default", "{controller=Finanzas}/{action=FacturasPorVencer}/{id?}");
        await SeedAsync();
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        File.Delete(_database);
    }

    [Fact]
    public async Task Panel_MuestraFacturasProximasYResumenDeVencimientos()
    {
        var html = await _client.GetStringAsync("/Finanzas/FacturasPorVencer");

        Assert.Contains("Facturas por vencer", html);
        Assert.Contains("Factura-001", html);
        Assert.Contains("Constructora Andina S.A.C.", html);
        Assert.Contains("≤ 7 días", html);
        Assert.Contains("16 – 30 días", html);
    }

    [Fact]
    public async Task Panel_FiltraPorClienteEstadoYFecha()
    {
        var porCliente = await _client.GetStringAsync($"/Finanzas/FacturasPorVencer?clienteId={_clienteDosId}");
        var porRango = await _client.GetStringAsync($"/Finanzas/FacturasPorVencer?fechaDesde={DateTime.Today.AddDays(2):yyyy-MM-dd}&fechaHasta={DateTime.Today.AddDays(7):yyyy-MM-dd}");
        var pagadas = await _client.GetStringAsync($"/Finanzas/FacturasPorVencer?estado=PAGADA");

        Assert.Contains("Factura-002", porCliente);
        Assert.DoesNotContain("Factura-001", porCliente);
        Assert.Contains("Factura-001", porRango);
        Assert.Contains("Factura-002", porRango);
        Assert.Contains("Factura-003", porRango);
        Assert.Contains("Factura-004", pagadas);
        Assert.DoesNotContain("Factura-001", pagadas);
    }

    [Fact]
    public async Task Panel_MuestraMensajesParaRangoInvalidoOVacioYSinFacturas()
    {
        var invalido = WebUtility.HtmlDecode(await _client.GetStringAsync(
            $"/Finanzas/FacturasPorVencer?fechaDesde={DateTime.Today.AddDays(10):yyyy-MM-dd}&fechaHasta={DateTime.Today:yyyy-MM-dd}"));
        var rangoVacio = WebUtility.HtmlDecode(await _client.GetStringAsync(
            $"/Finanzas/FacturasPorVencer?fechaDesde={DateTime.Today.AddDays(50):yyyy-MM-dd}&fechaHasta={DateTime.Today.AddDays(55):yyyy-MM-dd}"));
        var clienteVacio = WebUtility.HtmlDecode(await _client.GetStringAsync($"/Finanzas/FacturasPorVencer?clienteId=9999"));
        var estadoVacio = WebUtility.HtmlDecode(await _client.GetStringAsync("/Finanzas/FacturasPorVencer?estado=PAGADA&clienteId=9999"));

        Assert.Contains("La fecha inicial no puede ser mayor a la fecha final", invalido);
        Assert.Contains("No se encontraron facturas en el rango de fechas indicado", rangoVacio);
        Assert.Contains("No se encontraron facturas por vencer para el cliente seleccionado", clienteVacio);
        Assert.Contains("No se encontraron facturas por vencer para el cliente seleccionado", estadoVacio);
    }

    [Fact]
    public async Task CuentasPorCobrar_FiltraPorClienteEstadoYFechasDeEmisionYVencimiento()
    {
        var inicioEmision = DateTime.Today.AddDays(-28).ToString("yyyy-MM-dd");
        var porEmision = await _client.GetStringAsync(
            $"/Finanzas/CuentasPorCobrar?clienteId={_clienteUnoId}&estado=AL_DIA&fechaEmisionDesde={inicioEmision}&fechaEmisionHasta={inicioEmision}");
        var porVencimiento = await _client.GetStringAsync(
            $"/Finanzas/CuentasPorCobrar?fechaVencimientoDesde={DateTime.Today.AddDays(2):yyyy-MM-dd}&fechaVencimientoHasta={DateTime.Today.AddDays(2):yyyy-MM-dd}");
        var sinCoincidencias = WebUtility.HtmlDecode(await _client.GetStringAsync(
            "/Finanzas/CuentasPorCobrar?clienteId=9999&estado=PAGADA"));
        var fechasInvalidas = WebUtility.HtmlDecode(await _client.GetStringAsync(
            $"/Finanzas/CuentasPorCobrar?fechaEmisionDesde={DateTime.Today:yyyy-MM-dd}&fechaEmisionHasta={DateTime.Today.AddDays(-1):yyyy-MM-dd}"));

        Assert.Contains("Factura-001", porEmision);
        Assert.DoesNotContain("Factura-002", porEmision);
        Assert.Contains("badge-status", porEmision);
        Assert.Contains("prosegin-card", porEmision);
        Assert.Contains("Factura-001", porVencimiento);
        Assert.DoesNotContain("Factura-002", porVencimiento);
        Assert.Contains("Cuentas por Cobrar", porEmision);
        Assert.Contains("Facturas por Vencer", porEmision);
        Assert.Contains("Registro de Pago", porEmision);
        Assert.Contains("No se encontraron facturas con los criterios seleccionados", sinCoincidencias);
        Assert.Contains("La fecha inicial no puede ser mayor a la fecha final", fechasInvalidas);
    }

    [Fact]
    public async Task CuentasPorCobrar_MuestraDetallePermiteAbrirRegistroDePagoYDescargarPdf()
    {
        var detalle = await _client.GetStringAsync("/Finanzas/DetalleFactura?id=1");
        var lista = await _client.GetStringAsync("/Finanzas/CuentasPorCobrar");
        var pdf = await _client.GetAsync("/Finanzas/DescargarFactura?id=1&origen=CuentasPorCobrar");
        var sinPdf = await _client.GetAsync("/Finanzas/DescargarFactura?id=5&origen=CuentasPorCobrar");

        Assert.Contains("Detalle de factura", detalle);
        Assert.Contains("Constructora Andina S.A.C.", detalle);
        Assert.Contains("Saldo pendiente", detalle);
        Assert.Contains("facturaId=1", lista);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Redirect, sinPdf.StatusCode);
        Assert.StartsWith("/Finanzas/CuentasPorCobrar", sinPdf.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task DescargarFactura_DevuelvePdfYRedirigeCuandoNoHayDocumento()
    {
        var pdf = await _client.GetAsync("/Finanzas/DescargarFactura?id=1");
        var sinPdf = await _client.GetAsync("/Finanzas/DescargarFactura?id=5");
        var vista = WebUtility.HtmlDecode(await _client.GetStringAsync(sinPdf.Headers.Location!));

        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Factura-001.pdf", pdf.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(HttpStatusCode.Redirect, sinPdf.StatusCode);
        Assert.Contains("El documento electrónico no está disponible", vista);
    }

    [Fact]
    public async Task RegistroPago_ListaPendientesYPermiteBuscarPorFacturaOCliente()
    {
        var listado = await _client.GetStringAsync("/Finanzas/RegistroPago");
        var porFactura = await _client.GetStringAsync("/Finanzas/RegistroPago?busqueda=Factura-002");
        var porCliente = await _client.GetStringAsync("/Finanzas/RegistroPago?busqueda=Constructora");
        var invalida = WebUtility.HtmlDecode(await _client.GetStringAsync("/Finanzas/RegistroPago?busqueda=%40%40%23%23%25%25"));

        Assert.Contains("Factura-001", listado);
        Assert.DoesNotContain("Factura-004", listado);
        Assert.Contains("prosegin-page-header--accent", listado);
        Assert.Contains("btn-prosegin-primary", listado);
        Assert.Contains("finanzas.css", listado);
        Assert.Contains("Factura-002", porFactura);
        Assert.DoesNotContain("Factura-001", porFactura);
        Assert.Contains("Factura-001", porCliente);
        Assert.DoesNotContain("Factura-002", porCliente);
        Assert.Contains("Ingrese un número de factura o cliente válido", invalida);
    }

    [Fact]
    public async Task RegistrarPago_TotalYCubiertoParcialActualizaSaldoYEstado()
    {
        var total = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "1",
            ["Monto"] = "4500",
            ["FechaPago"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Transferencia",
            ["Referencia"] = "00123456"
        });
        Assert.Equal(HttpStatusCode.Redirect, total.StatusCode);

        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
            var factura = await context.Facturas.Include(item => item.Pagos).SingleAsync(item => item.Id == 1);
            Assert.Equal("Pagado", factura.EstadoPago);
            Assert.Single(factura.Pagos);
            Assert.Equal(4500m, factura.Pagos.Single().Monto);
            Assert.Equal("00123456", factura.Pagos.Single().Referencia);
        }

        var parcial = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "2",
            ["Monto"] = "1000",
            ["FechaPago"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Efectivo"
        });
        Assert.Equal(HttpStatusCode.Redirect, parcial.StatusCode);

        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
            var factura = await context.Facturas.Include(item => item.Pagos).SingleAsync(item => item.Id == 2);
            Assert.Equal("Pendiente", factura.EstadoPago);
            Assert.Equal(1000m, factura.Pagos.Sum(pago => pago.Monto));
            Assert.Equal(7900m, factura.Total - factura.Pagos.Sum(pago => pago.Monto));
        }

        var segundoParcialInvalido = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "2",
            ["Monto"] = "500",
            ["FechaPago"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Transferencia"
        });
        Assert.Contains(
            "el segundo pago debe cubrir obligatoriamente la totalidad del saldo pendiente",
            await LeerAlertaAsync(segundoParcialInvalido));

        var segundoTotal = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "2",
            ["Monto"] = "7900",
            ["FechaPago"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Transferencia"
        });
        Assert.Equal(HttpStatusCode.Redirect, segundoTotal.StatusCode);

        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
            var factura = await context.Facturas.Include(item => item.Pagos).SingleAsync(item => item.Id == 2);
            Assert.Equal("Pagado", factura.EstadoPago);
            Assert.Equal(2, factura.Pagos.Count);
            Assert.Equal(8900m, factura.Pagos.Sum(pago => pago.Monto));
        }
    }

    [Fact]
    public async Task RegistrarPago_RechazaMontoMayorFechaFuturaOCamposVacios()
    {
        var montoMayor = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "1",
            ["Monto"] = "5000",
            ["FechaPago"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Transferencia"
        });
        Assert.Contains("El monto del pago no puede ser mayor al saldo pendiente", await LeerAlertaAsync(montoMayor));

        var vacios = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "1",
            ["Monto"] = "",
            ["FechaPago"] = "",
            ["MetodoPago"] = ""
        });
        Assert.Contains("Complete los campos obligatorios", await LeerAlertaAsync(vacios));

        var futuro = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "1",
            ["Monto"] = "100",
            ["FechaPago"] = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Transferencia"
        });
        Assert.Contains("La fecha de pago no puede ser posterior a la fecha actual", await LeerAlertaAsync(futuro));

        var cero = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "1",
            ["Monto"] = "0",
            ["FechaPago"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Transferencia"
        });
        Assert.Contains("Ingrese un monto mayor a S/ 0.00", await LeerAlertaAsync(cero));

        var negativo = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "1",
            ["Monto"] = "-100",
            ["FechaPago"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Transferencia"
        });
        Assert.Contains("Ingrese un monto del pago válido", await LeerAlertaAsync(negativo));

        var sinFactura = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "0",
            ["Monto"] = "100",
            ["FechaPago"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Transferencia"
        });
        Assert.Contains("Seleccione una factura pendiente de pago", await LeerAlertaAsync(sinFactura));

        var yaPagada = await EnviarPagoAsync(new Dictionary<string, string>
        {
            ["FacturaId"] = "4",
            ["Monto"] = "100",
            ["FechaPago"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["MetodoPago"] = "Transferencia"
        });
        Assert.Contains("La factura ya se encuentra pagada", await LeerAlertaAsync(yaPagada));

        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        Assert.Empty(await context.PagosFactura.ToListAsync());
    }

    private async Task<HttpResponseMessage> EnviarPagoAsync(Dictionary<string, string> fields)
    {
        var html = await _client.GetStringAsync("/Finanzas/RegistroPago");
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value);
        return await _client.PostAsync("/Finanzas/RegistrarPago", new FormUrlEncodedContent(fields));
    }

    private async Task<string> LeerAlertaAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync(response.Headers.Location!));
        return html;
    }

    private async Task SeedAsync()
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        await context.Database.EnsureCreatedAsync();
        var clienteUno = new Cliente { Ruc = "20100000001", RazonSocial = "Constructora Andina S.A.C.", DireccionFiscal = "Av. Prueba 1" };
        var clienteDos = new Cliente { Ruc = "20100000002", RazonSocial = "Minera Del Sur S.A.", DireccionFiscal = "Av. Prueba 2" };
        context.Clientes.AddRange(clienteUno, clienteDos);
        await context.SaveChangesAsync();
        _clienteUnoId = clienteUno.Id;
        _clienteDosId = clienteDos.Id;

        var facturas = new[]
        {
            CrearFactura(clienteUno, "Factura-001", DateTime.Today.AddDays(2), "Pendiente", 4500m, "https://example.test/factura.pdf"),
            CrearFactura(clienteDos, "Factura-002", DateTime.Today.AddDays(4), "Pendiente", 8900m, "https://example.test/factura.pdf"),
            CrearFactura(clienteUno, "Factura-003", DateTime.Today.AddDays(7), "Pendiente", 2350m, "https://example.test/factura.pdf"),
            CrearFactura(clienteUno, "Factura-004", DateTime.Today.AddDays(11), "Pagado", 6000m, "https://example.test/factura.pdf"),
            CrearFactura(clienteUno, "Factura-005", DateTime.Today.AddDays(15), "Pendiente", 3200m, null),
            CrearFactura(clienteUno, "Factura-006", DateTime.Today.AddDays(20), "Pendiente", 100m, "https://example.test/factura.pdf"),
            CrearFactura(clienteUno, "Factura-007", DateTime.Today.AddDays(30), "Pendiente", 100m, "https://example.test/factura.pdf"),
            CrearFactura(clienteUno, "Factura-008", DateTime.Today.AddDays(-1), "Vencido", 100m, "https://example.test/factura.pdf")
        };
        context.Facturas.AddRange(facturas);
        await context.SaveChangesAsync();
    }

    private static Factura CrearFactura(Cliente cliente, string numero, DateTime vencimiento, string estado, decimal total, string? enlacePdf)
    {
        var correlativo = $"COT-{numero}";
        var orden = new OrdenVenta
        {
            EstadoLogistico = "Entregado",
            Cotizacion = new Cotizacion
            {
                Correlativo = correlativo,
                Cliente = cliente,
                Estado = "Aprobada",
                Total = total
            }
        };
        return new Factura
        {
            OrdenVenta = orden,
            Cliente = cliente,
            Serie = "F001",
            Numero = int.Parse(numero.AsSpan("Factura-".Length)),
            NumeroFactura = numero,
            FechaEmision = vencimiento.AddDays(-30),
            FechaVencimiento = vencimiento,
            EstadoPago = estado,
            EstadoEmision = "Aceptado",
            Total = total,
            EnlacePdf = enlacePdf
        };
    }

    private sealed class PdfResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("%PDF-1.7 invoice"u8.ToArray())
            });
    }
}
