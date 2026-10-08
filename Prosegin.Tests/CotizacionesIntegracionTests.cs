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
using Prosegin.Data.Validation;
using Prosegin.Web.Controllers;
using Prosegin.Web.Services;
using Xunit;

namespace Prosegin.Tests;

// Usa las rutas, el model binding, antiforgery, Razor y persistencia relacional reales.
// Cada prueba tiene su archivo SQLite propio; nunca ejecuta los seeders de desarrollo.
public sealed class CotizacionesTestHost : IAsyncLifetime
{
    private WebApplication _app = null!;
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"prosegin-hu31-{Guid.NewGuid():N}.db");
    public HttpClient Client { get; private set; } = null!;
    public Uri BaseAddress { get; private set; } = null!;
    public SunatPrueba Sunat { get; } = new();

    public async Task InitializeAsync()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Prosegin.slnx"))) root = root.Parent;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(CotizacionesController).Assembly.GetName().Name,
            ContentRootPath = Path.Combine(root!.FullName, "Prosegin.Web"),
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(CotizacionesController).Assembly);
        builder.Services.AddDbContext<ProseginDbContext>(options => options.UseSqlite($"Data Source={_database};Pooling=False"));
        builder.Services.AddSingleton<ISunatService>(Sunat);
        _app = builder.Build();
        _app.UseStaticFiles();
        _app.MapControllerRoute("default", "{controller=Clientes}/{action=Index}/{id?}");
        await WithDatabaseAsync(async context =>
        {
            await context.Database.EnsureCreatedAsync();
            context.Clientes.AddRange(
                new Cliente { Id = 1, Ruc = "20123456789", RazonSocial = "Cliente de prueba A", DireccionFiscal = "Dirección prueba" },
                new Cliente { Id = 2, Ruc = "20987654321", RazonSocial = "Cliente de prueba B", DireccionFiscal = "Dirección prueba" });
            context.Productos.AddRange(
                new Producto { Id = 1, Sku = "HU31-CASCO", Nombre = "Casco de prueba", UnidadMedida = "UND", Categoria = "Cabeza", CostoReferencial = 10m },
                new Producto { Id = 2, Sku = "HU31-GUANTE", Nombre = "Guante de prueba", UnidadMedida = "PAR", Categoria = "Manos", CostoReferencial = 0.25m },
                new Producto { Id = 3, Sku = "HU31-INACTIVO", Nombre = "Producto inactivo", Activo = false, CostoReferencial = 99m });
            await context.SaveChangesAsync();
        });
        await _app.StartAsync();
        BaseAddress = new Uri(_app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = BaseAddress };
    }

    public async Task WithDatabaseAsync(Func<ProseginDbContext, Task> action)
    {
        using var scope = _app.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ProseginDbContext>());
    }

    public async Task<T> WithDatabaseAsyncResultAsync<T>(Func<ProseginDbContext, Task<T>> action)
    {
        using var scope = _app.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ProseginDbContext>());
    }

    public async Task<HttpResponseMessage> PostAsync(Dictionary<string, string> fields, string action = "Create", string controller = "Cotizaciones")
    {
        var html = await Client.GetStringAsync("/Cotizaciones/Create?clienteId=1");
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value);
        return await Client.PostAsync($"/{controller}/{action}", new FormUrlEncodedContent(fields));
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
        File.Delete(_database);
    }

    public sealed class SunatPrueba : ISunatService
    {
        public string Direccion { get; set; } = "Dirección prueba";
        public string Estado { get; set; } = "ACTIVO";
        public Task<SunatConsultaResult> ConsultarRucAsync(string ruc, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SunatConsultaResult(true, null, ruc, "Cliente verificado", Direccion,
                null, null, null, null, Estado, "HABIDO", null, null, "Prueba", true, Estado == "ACTIVO"));
        public bool ValidarFormatoRuc(string ruc, out string? mensajeError) { mensajeError = null; return true; }
    }
}

public class CotizacionesIntegracionTests : IAsyncLifetime
{
    private readonly CotizacionesTestHost _host = new();
    public Task InitializeAsync() => _host.InitializeAsync();
    public Task DisposeAsync() => _host.DisposeAsync();

    private static Dictionary<string, string> ProductForm(string quantity = "2", int productId = 1) => new()
    {
        ["ClienteId"] = "1", ["CondicionPago"] = "7 días",
        ["ProductosDisponibles[0].ProductoId"] = productId.ToString(),
        ["ProductosDisponibles[0].Seleccionado"] = "true",
        ["ProductosDisponibles[0].Cantidad"] = quantity,
        ["ProductosDisponibles[0].MargenPorcentaje"] = "0"
    };

    private async Task<int> SeedCotizacionAsync(string estado, DateTime fechaEmision, DateTime? fechaVencimiento, decimal precio = 12m)
    {
        var quoteId = 0;
        await _host.WithDatabaseAsync(async context =>
        {
            var quote = new Cotizacion
            {
                Correlativo = $"COT-2026-TEST-{Guid.NewGuid():N}",
                ClienteId = 1,
                FechaEmision = fechaEmision,
                FechaVencimiento = fechaVencimiento,
                Estado = estado,
                CondicionPago = "7 días",
                Subtotal = precio * 2,
                Igv = CotizacionPrecioRules.CalcularIgv(precio * 2),
                Total = CotizacionPrecioRules.CalcularTotal(precio * 2, CotizacionPrecioRules.CalcularIgv(precio * 2)),
                Detalles = new List<CotizacionDetalle>
                {
                    new()
                    {
                        ProductoId = 1,
                        Cantidad = 2,
                        CostoProveedorReferencial = 10m,
                        MargenDeseado = (precio - 10m) / 10m,
                        PrecioVentaCalculado = precio,
                        Subtotal = precio * 2
                    }
                }
            };
            context.Cotizaciones.Add(quote);
            await context.SaveChangesAsync();
            quoteId = quote.Id;
        });
        return quoteId;
    }

    [Fact]
    public async Task Grabar_PersisteProductosCantidadesImportesYMensaje()
    {
        var form = ProductForm();
        form["ProductosDisponibles[0].CostoReferencial"] = "999";
        var response = await _host.PostAsync(form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("cotizacionId=", response.Headers.Location!.OriginalString);
        var html = WebUtility.HtmlDecode(await _host.Client.GetStringAsync(response.Headers.Location));
        Assert.Contains("Productos guardados correctamente", html);
        Assert.Contains("data-cotizacion-guardada=\"true\"", html);
        Assert.Contains("id=\"generarPdf\"", html);
        Assert.Contains("Vista Previa de Cotización", html);
        Assert.Contains("No disponible", html);
        await _host.WithDatabaseAsync(async context =>
        {
            var quote = Assert.Single(await context.Cotizaciones.Include(q => q.Detalles).AsNoTracking().ToListAsync());
            var line = Assert.Single(quote.Detalles);
            Assert.Equal(1, quote.ClienteId);
            Assert.Equal(1, line.ProductoId);
            Assert.Equal(2, line.Cantidad);
            Assert.Equal(10m, line.CostoProveedorReferencial);
            Assert.Equal(20m, line.Subtotal);
            Assert.Equal(20m, quote.Subtotal);
            Assert.Equal(3.60m, quote.Igv);
            Assert.Equal(23.60m, quote.Total);
        });
    }

    [Fact]
    public async Task DescargarPdf_GeneraArchivoYMarcaLaCotizacionComoEnviada()
    {
        await _host.WithDatabaseAsync(async context =>
        {
            var product = await context.Productos.SingleAsync(p => p.Id == 1);
            product.RutaFichaTecnicaPdf = "/uploads/fichas/HU31-CASCO.pdf";
            product.NombreArchivoPdf = "HU31-CASCO.pdf";
            await context.SaveChangesAsync();
        });
        var saveResponse = await _host.PostAsync(ProductForm());
        Assert.Equal(HttpStatusCode.Redirect, saveResponse.StatusCode);
        var location = saveResponse.Headers.Location!;
        var cotizacionId = int.Parse(
            Regex.Match(location.OriginalString, @"[?&]cotizacionId=(\d+)").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        var preview = WebUtility.HtmlDecode(await _host.Client.GetStringAsync(location));
        Assert.Contains("HU31-CASCO", preview);
        Assert.Contains("target=\"_blank\"", preview);
        Assert.Contains("Abrir ficha técnica", preview);

        var downloadResponse = await _host.PostAsync(new() { ["cotizacionId"] = cotizacionId.ToString() }, "DescargarPdf");

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith($"COT-{DateTime.UtcNow:yyyy}-",
            downloadResponse.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
        Assert.Contains("/Catalogo/VerFicha/1", System.Text.Encoding.Latin1.GetString(bytes));
        await _host.WithDatabaseAsync(async context =>
        {
            var quote = await context.Cotizaciones.SingleAsync(q => q.Id == cotizacionId);
            Assert.Equal("Enviada", quote.Estado);
            Assert.NotNull(quote.FechaVencimiento);
            Assert.InRange((quote.FechaVencimiento!.Value - quote.FechaEmision).TotalHours, 47.99, 48.01);
        });
        var sentHtml = WebUtility.HtmlDecode(await _host.Client.GetStringAsync(location));
        Assert.Contains("ENVIADA", sentHtml);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("2.8")]
    [InlineData("2147483648")]
    public async Task Grabar_RechazaCantidadInvalidaSinPersistir(string quantity)
    {
        var response = await _host.PostAsync(ProductForm(quantity));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("data-post-invalido=\"true\"", html);
        Assert.DoesNotContain("Productos guardados correctamente", html);
        Assert.Contains("validation-summary-errors", html);
        await AssertNoQuotesAsync();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(999)]
    public async Task Grabar_RechazaProductoInactivoOInexistente(int productId)
    {
        var response = await _host.PostAsync(ProductForm(productId: productId));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("ya no están disponibles", html);
        await AssertNoQuotesAsync();
    }

    [Fact]
    public async Task Grabar_RechazaProductoDuplicado()
    {
        var form = ProductForm();
        form["ProductosDisponibles[1].ProductoId"] = "1";
        form["ProductosDisponibles[1].Seleccionado"] = "true";
        form["ProductosDisponibles[1].Cantidad"] = "3";
        form["ProductosDisponibles[1].MargenPorcentaje"] = "0";
        var response = await _host.PostAsync(form);
        Assert.Contains("mismo producto más de una vez", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        await AssertNoQuotesAsync();
    }

    [Fact]
    public async Task Grabar_RechazaListaVacia()
    {
        var response = await _host.PostAsync(new() { ["ClienteId"] = "1", ["CondicionPago"] = "7 días" });
        Assert.Contains("Selecciona al menos un producto", await response.Content.ReadAsStringAsync());
        await AssertNoQuotesAsync();
    }

    [Fact]
    public async Task Grabar_RedondeaIgualQueLaPantalla()
    {
        var response = await _host.PostAsync(ProductForm("1", 2));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await _host.WithDatabaseAsync(async context =>
        {
            var quote = await context.Cotizaciones.AsNoTracking().SingleAsync();
            Assert.Equal(0.25m, quote.Subtotal);
            Assert.Equal(0.05m, quote.Igv);
            Assert.Equal(0.30m, quote.Total);
        });
    }

    [Fact]
    public async Task Bandeja_FiltraEstadosBusquedaYOrdenaPorEmision()
    {
        var now = DateTime.UtcNow;
        await SeedCotizacionAsync("Enviada", now.AddHours(-3), now.AddHours(45));
        await SeedCotizacionAsync("Enviada", now.AddHours(-55), now.AddHours(-7));
        await SeedCotizacionAsync("Aprobada", now.AddDays(-2), null);
        await SeedCotizacionAsync("Borrador", now, null);

        var allHtml = WebUtility.HtmlDecode(await _host.Client.GetStringAsync("/Cotizaciones"));
        Assert.Contains("Cotizaciones", allHtml);
        Assert.Contains("Enviada", allHtml);
        Assert.Contains("Vencida", allHtml);
        Assert.Contains("Aprobada", allHtml);
        Assert.DoesNotContain("Borrador", allHtml);
        Assert.Contains("COT-2026-TEST-", allHtml);
        Assert.Contains("Cotizaciones", allHtml);

        var expiredHtml = WebUtility.HtmlDecode(await _host.Client.GetStringAsync("/Cotizaciones?estado=Vencida"));
        Assert.Contains("Expiró hace", expiredHtml);
        Assert.Contains("Actualizar cotización", expiredHtml);
        Assert.DoesNotContain("Cerrada por aceptación", expiredHtml);

        var searchHtml = WebUtility.HtmlDecode(await _host.Client.GetStringAsync("/Cotizaciones?busqueda=Cliente%20de%20prueba%20A"));
        Assert.Contains("COT-2026-TEST-", searchHtml);
        Assert.Contains("Cliente de prueba A", searchHtml);

        var quoteId = await _host.WithDatabaseAsyncResultAsync(async context =>
            await context.Cotizaciones.Where(c => c.Estado == "Aprobada").Select(c => c.Id).SingleAsync());
        var approvedPdf = await _host.Client.GetAsync($"/Cotizaciones/VerPdf/{quoteId}");
        Assert.Equal(HttpStatusCode.OK, approvedPdf.StatusCode);
        Assert.Equal("inline", approvedPdf.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("application/pdf", approvedPdf.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Bandeja_BuscaPorCorrelativo()
    {
        var quoteId = await SeedCotizacionAsync("Enviada", DateTime.UtcNow, DateTime.UtcNow.AddHours(48));
        var correlativo = await _host.WithDatabaseAsyncResultAsync(async context =>
            await context.Cotizaciones.Where(c => c.Id == quoteId).Select(c => c.Correlativo).SingleAsync());

        var html = WebUtility.HtmlDecode(await _host.Client.GetStringAsync($"/Cotizaciones?busqueda={Uri.EscapeDataString(correlativo)}"));

        Assert.Contains(correlativo, html);
        Assert.Contains("Mostrando 1 de 1 cotizaciones emitidas", html);
    }

    [Fact]
    public async Task Actualizar_CotizacionVencidaCreaUnaNuevaConPrecioActualYConservaLaAnterior()
    {
        var oldQuoteId = await SeedCotizacionAsync("Enviada", DateTime.UtcNow.AddHours(-60), DateTime.UtcNow.AddHours(-12), 12m);
        await _host.WithDatabaseAsync(async context =>
        {
            var product = await context.Productos.SingleAsync(p => p.Id == 1);
            product.CostoReferencial = 20m;
            await context.SaveChangesAsync();
        });

        var response = await _host.PostAsync(new() { ["id"] = oldQuoteId.ToString() }, "Actualizar");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("estado=Enviada", response.Headers.Location!.OriginalString);
        await _host.WithDatabaseAsync(async context =>
        {
            var quotes = await context.Cotizaciones
                .Include(q => q.Detalles)
                .OrderBy(q => q.Id)
                .AsNoTracking()
                .ToListAsync();
            Assert.Equal(2, quotes.Count);
            var previous = Assert.Single(quotes.Where(q => q.Id == oldQuoteId));
            var renewed = Assert.Single(quotes.Where(q => q.Id != oldQuoteId));
            Assert.Equal("Enviada", previous.Estado);
            Assert.Equal(12m, previous.Detalles.Single().PrecioVentaCalculado);
            Assert.Equal(1, renewed.ClienteId);
            Assert.Equal("Enviada", renewed.Estado);
            Assert.NotEqual(previous.Correlativo, renewed.Correlativo);
            Assert.Matches(@"^COT-\d{4}-\d{4,}$", renewed.Correlativo);
            Assert.InRange((renewed.FechaVencimiento!.Value - renewed.FechaEmision).TotalHours, 47.99, 48.01);
            var renewedLine = Assert.Single(renewed.Detalles);
            Assert.Equal(2, renewedLine.Cantidad);
            Assert.Equal(20m, renewedLine.CostoProveedorReferencial);
            Assert.Equal(24m, renewedLine.PrecioVentaCalculado);
            Assert.Equal(48m, renewed.Subtotal);
            Assert.Equal(56.64m, renewed.Total);
        });
    }

    [Fact]
    public async Task Actualizar_RechazaCotizacionAunVigente()
    {
        var activeQuoteId = await SeedCotizacionAsync("Enviada", DateTime.UtcNow, DateTime.UtcNow.AddHours(48));

        var response = await _host.PostAsync(new() { ["id"] = activeQuoteId.ToString() }, "Actualizar");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = WebUtility.HtmlDecode(await _host.Client.GetStringAsync(response.Headers.Location));
        Assert.Contains("Solo se pueden actualizar cotizaciones vencidas", html);
        await _host.WithDatabaseAsync(async context => Assert.Single(await context.Cotizaciones.ToListAsync()));
    }

    [Fact]
    public async Task Grabar_VariosProductosConservaCantidadesYRedondeo()
    {
        var form = ProductForm("3");
        form["ProductosDisponibles[1].ProductoId"] = "2";
        form["ProductosDisponibles[1].Seleccionado"] = "true";
        form["ProductosDisponibles[1].Cantidad"] = "1";
        form["ProductosDisponibles[1].MargenPorcentaje"] = "0";
        Assert.Equal(HttpStatusCode.Redirect, (await _host.PostAsync(form)).StatusCode);
        await _host.WithDatabaseAsync(async context =>
        {
            var quote = await context.Cotizaciones.Include(q => q.Detalles).AsNoTracking().SingleAsync();
            Assert.Equal(2, quote.Detalles.Count);
            Assert.Equal(3, quote.Detalles.Single(d => d.ProductoId == 1).Cantidad);
            Assert.Equal(1, quote.Detalles.Single(d => d.ProductoId == 2).Cantidad);
            Assert.Equal(30.25m, quote.Subtotal);
            Assert.Equal(5.45m, quote.Igv);
            Assert.Equal(35.70m, quote.Total);
        });
    }

    [Fact]
    public async Task CatalogoGeneral_ConservaElTraspasoDeLaSeleccionAlCliente()
    {
        var html = WebUtility.HtmlDecode(await _host.Client.GetStringAsync("/Catalogo"));
        Assert.Contains("HU31-CASCO", html);
        Assert.DoesNotContain("HU31-INACTIVO", html);
        Assert.DoesNotContain("catalog-cart", html);
        var script = await _host.Client.GetStringAsync("/js/cotizacion-productos.js");
        Assert.Contains("prosegin.quote.pending.v1", script);
    }

    [Fact]
    public async Task Catalogo_MuestraSoloActivosYTextosDeLaHU()
    {
        var html = WebUtility.HtmlDecode(await _host.Client.GetStringAsync("/Cotizaciones/Create?clienteId=2"));
        Assert.Contains("HU31-CASCO", html);
        Assert.DoesNotContain("HU31-INACTIVO", html);
        Assert.Contains("+Agregar", html);
        Assert.Contains("Total General", html);
        Assert.Contains("Opciones", html);
        Assert.Contains("data-cliente-id=\"2\"", html);
        Assert.Contains("id=\"generarPdf\"", html);
        Assert.DoesNotContain("id=\"cotizacionPdfModal\"", html);
        var pdfButton = Regex.Match(html, "<button[^>]*id=\"generarPdf\"[^>]*>").Value;
        Assert.Contains("disabled", pdfButton);
        Assert.Contains("cotizacion-productos.js", html);
        Assert.DoesNotContain("select2", html);
    }

    [Fact]
    public async Task ActualizarSunat_NoMarcaLaCotizacionComoGuardada()
    {
        _host.Sunat.Direccion = "Dirección nueva";
        var response = await _host.PostAsync(new() { ["clienteId"] = "1" }, "ActualizarDesdeSunat");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = WebUtility.HtmlDecode(await _host.Client.GetStringAsync(response.Headers.Location));
        Assert.Contains("Los datos fiscales fueron actualizados", html);
        Assert.Contains("data-cotizacion-guardada=\"false\"", html);
        await AssertNoQuotesAsync();
    }

    [Fact]
    public async Task Grabar_ConservaLaValidacionFiscal()
    {
        _host.Sunat.Estado = "BAJA";
        var response = await _host.PostAsync(ProductForm());
        Assert.Contains("no figura ACTIVO y HABIDO", await response.Content.ReadAsStringAsync());
        await AssertNoQuotesAsync();
    }

    [Fact]
    public async Task Grabar_PersisteVariasFilasYCalculaElTotalConjunto()
    {
        var form = ProductForm("5");
        form["ProductosDisponibles[1].ProductoId"] = "2";
        form["ProductosDisponibles[1].Seleccionado"] = "true";
        form["ProductosDisponibles[1].Cantidad"] = "3";
        form["ProductosDisponibles[1].MargenPorcentaje"] = "0";

        var response = await _host.PostAsync(form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await _host.WithDatabaseAsync(async context =>
        {
            var quote = await context.Cotizaciones.Include(q => q.Detalles).AsNoTracking().SingleAsync();
            Assert.Equal(2, quote.Detalles.Count);
            Assert.Equal(5, quote.Detalles.Single(d => d.ProductoId == 1).Cantidad);
            Assert.Equal(3, quote.Detalles.Single(d => d.ProductoId == 2).Cantidad);
            Assert.Equal(50.75m, quote.Subtotal);
            Assert.Equal(9.14m, quote.Igv);
            Assert.Equal(59.89m, quote.Total);
        });
    }

    [Fact]
    public async Task Grabar_RevalidaUnProductoDesactivadoDespuesDeAbrirElCatalogo()
    {
        var catalogo = await _host.Client.GetStringAsync("/Cotizaciones/Create?clienteId=1");
        Assert.Contains("HU31-CASCO", catalogo);
        await _host.WithDatabaseAsync(async context =>
        {
            (await context.Productos.SingleAsync(p => p.Id == 1)).Activo = false;
            await context.SaveChangesAsync();
        });

        var response = await _host.PostAsync(ProductForm());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("ya no están disponibles en el catálogo", html);
        var seleccionDisponible = Regex.Match(html,
            "<input[^>]*type=\"checkbox\"[^>]*name=\"ProductosDisponibles\\[0\\]\\.Seleccionado\"[^>]*>").Value;
        Assert.NotEmpty(seleccionDisponible);
        Assert.DoesNotContain("checked", seleccionDisponible);
        Assert.Matches("name=\"ProductosDisponibles\\[0\\]\\.ProductoId\"[^>]*value=\"2\"", html);
        await AssertNoQuotesAsync();
    }

    [Fact]
    public async Task Grabar_ConservaLaCantidadInvalidaEnSuProductoSiCambiaElOrdenDelCatalogo()
    {
        var form = ProductForm("1");
        form["ProductosDisponibles[0].Seleccionado"] = "false";
        form["ProductosDisponibles[1].ProductoId"] = "2";
        form["ProductosDisponibles[1].Seleccionado"] = "true";
        form["ProductosDisponibles[1].Cantidad"] = "1.5";
        form["ProductosDisponibles[1].MargenPorcentaje"] = "0";
        await _host.WithDatabaseAsync(async context =>
        {
            (await context.Productos.SingleAsync(p => p.Id == 1)).Nombre = "Z casco renombrado";
            await context.SaveChangesAsync();
        });

        var response = await _host.PostAsync(form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Matches("name=\"ProductosDisponibles\\[0\\]\\.ProductoId\"[^>]*value=\"2\"", html);
        var cantidad = Regex.Match(html,
            "<input[^>]*name=\"ProductosDisponibles\\[0\\]\\.Cantidad\"[^>]*>").Value;
        Assert.True(cantidad.Contains("value=\"1.5\""), cantidad);
        var seleccion = Regex.Match(html,
            "<input[^>]*type=\"checkbox\"[^>]*name=\"ProductosDisponibles\\[0\\]\\.Seleccionado\"[^>]*>").Value;
        Assert.Contains("checked", seleccion);
        Assert.Contains("validation-summary-errors", html);
        await AssertNoQuotesAsync();
    }

    [Theory]
    [InlineData("12.50", "25", 25.00, 4.50, 29.50)]
    [InlineData("25.00", "150", 50.00, 9.00, 59.00)]
    public async Task Grabar_PrecioAjustadoConservaMargenYCostoMaestro(string precio, string margen,
        decimal subtotal, decimal igv, decimal total)
    {
        var form = ProductForm();
        form["ProductosDisponibles[0].PrecioUnitario"] = precio;
        form["ProductosDisponibles[0].MargenPorcentaje"] = margen;
        Assert.Equal(HttpStatusCode.Redirect, (await _host.PostAsync(form)).StatusCode);
        await _host.WithDatabaseAsync(async context =>
        {
            var quote = await context.Cotizaciones.Include(q => q.Detalles).AsNoTracking().SingleAsync();
            var line = Assert.Single(quote.Detalles);
            Assert.Equal(subtotal, quote.Subtotal);
            Assert.Equal(igv, quote.Igv);
            Assert.Equal(total, quote.Total);
            Assert.Equal(decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture), line.PrecioVentaCalculado);
            Assert.Equal(decimal.Parse(margen, System.Globalization.CultureInfo.InvariantCulture) / 100m, line.MargenDeseado);
            Assert.Equal(10m, (await context.Productos.SingleAsync(p => p.Id == 1)).CostoReferencial);
        });
    }

    [Theory]
    [InlineData("9.99")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task Grabar_RechazaPrecioMenorAlCostoSinPersistir(string precio)
    {
        var form = ProductForm();
        form["ProductosDisponibles[0].PrecioUnitario"] = precio;
        var response = await _host.PostAsync(form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("El precio cotizado no puede ser menor al costo base registrado",
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        await AssertNoQuotesAsync();
    }

    [Fact]
    public async Task Catalogo_EditarProductoConservaElFlujoAgregadoEnTest()
    {
        var html = await _host.Client.GetStringAsync("/Catalogo");
        Assert.Contains("editProductModal", html);
        Assert.Contains("populateEditModal(this)", html);
        var response = await _host.PostAsync(new()
        {
            ["Id"] = "1", ["Nombre"] = "Casco actualizado", ["Categoria"] = "Protección cabeza", ["Precio"] = "11.50"
        }, "EditarProducto", "Catalogo");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await _host.WithDatabaseAsync(async context =>
        {
            var product = await context.Productos.AsNoTracking().SingleAsync(p => p.Id == 1);
            Assert.Equal("CASCO ACTUALIZADO", product.Nombre);
            Assert.Equal("Protección cabeza", product.Categoria);
            Assert.Equal(11.50m, product.CostoReferencial);
        });
    }

    private Task AssertNoQuotesAsync() => _host.WithDatabaseAsync(async context =>
    {
        Assert.Empty(await context.Cotizaciones.ToListAsync());
        Assert.Empty(await context.CotizacionDetalles.ToListAsync());
    });
}
