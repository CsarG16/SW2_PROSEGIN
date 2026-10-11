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
using Prosegin.Web.Services;
using Xunit;

namespace Prosegin.Tests;

// Integra rutas HTTP, controlador, servicio y la vista Razor real sin ejecutar
// los seeders ni conectarse a la base de datos de desarrollo.
public class PedidosIntegracionTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"prosegin-pedidos-{Guid.NewGuid():N}.db");

    public async Task InitializeAsync()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz != null && !File.Exists(Path.Combine(raiz.FullName, "Prosegin.slnx")))
            raiz = raiz.Parent;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PedidosController).Assembly.GetName().Name,
            ContentRootPath = Path.Combine(raiz!.FullName, "Prosegin.Web"),
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(PedidosController).Assembly);
        builder.Services.AddDbContext<ProseginDbContext>(options =>
            options.UseSqlite($"Data Source={_database};Pooling=False"));
        builder.Services.AddScoped<IPedidosService, PedidosService>();
        _app = builder.Build();
        _app.MapControllerRoute("default", "{controller=Pedidos}/{action=Index}/{id?}");
        await SeedPedidosAsync();
        await _app.StartAsync();
        var direcciones = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        _client = new HttpClient { BaseAddress = new Uri(direcciones.Addresses.Single()) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        File.Delete(_database);
    }

    [Fact]
    public async Task Paginacion_NavegaSinRepetirPedidos_YConservaOrden()
    {
        var primera = await _client.GetStringAsync("/Pedidos");
        var segunda = await _client.GetStringAsync(Enlace(primera, "aria-label", "Página siguiente"));

        Assert.Equal(5, Pedidos(primera).Length);
        Assert.Equal(3, Pedidos(segunda).Length);
        Assert.Equal(new[] { "0842", "0839", "0831", "0824", "0845", "0835", "0820", "0848" },
            Pedidos(primera).Concat(Pedidos(segunda)).Select(id => id.Split('-').Last()));
        Assert.Equal(Pedidos(primera), Pedidos(await _client.GetStringAsync(Enlace(segunda, "aria-label", "Página anterior"))));
        Assert.Contains("Página 2 de 2", segunda);
        Assert.Matches("<button[^>]*disabled[^>]*aria-label=\"Página siguiente\"", segunda);
    }

    [Theory]
    [InlineData("?termino=20601928471&estado=EN_PREPARACION&pagina=2", 1)]
    [InlineData("?termino=NO_EXISTE&estado=POR_PREPARAR&pagina=99", 0)]
    public async Task Limpiar_DesdeUrlFiltrada_RecuperaTodosLosPedidos(string filtros, int coincidencias)
    {
        var filtrada = await _client.GetStringAsync("/Pedidos" + filtros);
        Assert.Equal(coincidencias, Pedidos(filtrada).Length);

        var enlace = Enlace(filtrada, "id", "btnClearFilters");
        Assert.DoesNotContain('?', enlace);
        var general = await _client.GetStringAsync(enlace);
        var siguiente = await _client.GetStringAsync(Enlace(general, "aria-label", "Página siguiente"));
        Assert.Equal(8, Pedidos(general).Concat(Pedidos(siguiente)).Distinct().Count());
        Assert.Contains("Página 1 de 2", general);
    }

    [Fact]
    public async Task Paginacion_ConservaBusqueda_AlCambiarPagina()
    {
        var primera = await _client.GetStringAsync("/Pedidos?termino=COT-2026&estado=ALL");
        var enlace = Enlace(primera, "aria-label", "Página siguiente");
        Assert.Contains("termino=COT-2026", enlace);
        Assert.Contains("estado=ALL", enlace);
        Assert.Contains("pagina=2", enlace);
        Assert.Equal(3, Pedidos(await _client.GetStringAsync(enlace)).Length);
    }

    [Fact]
    public async Task CambiarEstado_ConservaBusqueda_YVuelveALaPrimeraPagina()
    {
        var pagina = await _client.GetStringAsync("/Pedidos?termino=20601928471&pagina=2");
        var enlace = Enlace(pagina, "data-status", "EN_PREPARACION");
        Assert.Contains("termino=20601928471", enlace);
        Assert.DoesNotContain("pagina=", enlace);
        var filtrada = await _client.GetStringAsync(enlace);
        Assert.Equal(new[] { "COT-2026-0839" }, Pedidos(filtrada));
    }

    [Theory]
    [InlineData(-3, "0842")]
    [InlineData(99, "0835")]
    public async Task Paginacion_FueraDeRango_UsaUnaPaginaValida(int pagina, string primerPedido)
    {
        var html = await _client.GetStringAsync($"/Pedidos?pagina={pagina}");
        Assert.EndsWith(primerPedido, Pedidos(html)[0]);
    }

    [Fact]
    public async Task PedidosPersistidos_MuestranEntregaPendienteSinInventarUrgencia()
    {
        var html = await _client.GetStringAsync("/Pedidos");
        Assert.DoesNotContain("Entrega &le; 24h", html);
        Assert.Matches("id=\"kpiUrgentText\">\\s*0 despachos", html);
        Assert.Contains("Por programar", html);

        var detalle = await _client.GetStringAsync("/Pedidos/Detalle/COT-2026-0842");
        Assert.Contains("\"esUrgenteMenor24h\":false", detalle);
    }

    [Fact]
    public async Task DetalleDePedidoPorPreparar_AbreAbastecimientoConTarifasAutorizadas()
    {
        var html = await _client.GetStringAsync("/Pedidos");

        Assert.Contains("COT-2026-0842", html);
        Assert.Contains("GENERAR ORDEN DE COMPRA A PROVEEDORES", html);
        var abastecimiento = await _client.GetStringAsync("/Abastecimiento/Index?id=1");
        Assert.Contains("Abastecimiento de Pedido: COT-2026-0842", abastecimiento);
        Assert.Contains("Proveedor Dos S.A.C.", abastecimiento);
        Assert.Contains("S/ 10.25", abastecimiento);
        Assert.Contains("S/ 22.75", abastecimiento);
        Assert.Matches("<option value=\"1\"[^>]*selected=\"selected\"", abastecimiento);
    }

    [Fact]
    public async Task GenerarOrdenesCompra_CreaComprasConCostosRecalculadosYCambiaEstado()
    {
        var abastecimientoHtml = await _client.GetStringAsync("/Abastecimiento/Index?id=1");
        var tokenInput = Regex.Match(abastecimientoHtml, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        var token = WebUtility.HtmlDecode(Regex.Match(tokenInput, "value=\"([^\"]+)\"").Groups[1].Value);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ordenVentaId"] = "1",
            ["selecciones[0].ProductoId"] = "1",
            ["selecciones[0].ProveedorId"] = "1",
            ["selecciones[1].ProductoId"] = "2",
            ["selecciones[1].ProveedorId"] = "2"
        });

        var response = await _client.PostAsync("/Abastecimiento/GenerarOrdenesCompra", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("COT-2026-0842", await response.Content.ReadAsStringAsync());
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var order = await context.OrdenesVenta
            .Include(ov => ov.OrdenesCompra)
                .ThenInclude(oc => oc.Detalles)
            .SingleAsync(ov => ov.Id == 1);
        Assert.Equal("EnPreparacion", order.EstadoLogistico);
        Assert.Equal(2, order.OrdenesCompra.Count);
        Assert.Equal(25.00m, order.OrdenesCompra.Single(oc => oc.ProveedorId == 1).Total);
        Assert.Equal(68.25m, order.OrdenesCompra.Single(oc => oc.ProveedorId == 2).Total);
        Assert.Equal(2, order.OrdenesCompra.Sum(oc => oc.Detalles.Count));
    }

    [Fact]
    public async Task GenerarOrdenesCompra_RechazaProveedorNoAutorizadoSinCambiarPedido()
    {
        var abastecimientoHtml = await _client.GetStringAsync("/Abastecimiento/Index?id=1");
        var tokenInput = Regex.Match(abastecimientoHtml, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        var token = WebUtility.HtmlDecode(Regex.Match(tokenInput, "value=\"([^\"]+)\"").Groups[1].Value);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ordenVentaId"] = "1",
            ["selecciones[0].ProductoId"] = "1",
            ["selecciones[0].ProveedorId"] = "999",
            ["selecciones[1].ProductoId"] = "2",
            ["selecciones[1].ProveedorId"] = "2"
        });

        var response = await _client.PostAsync("/Abastecimiento/GenerarOrdenesCompra", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var order = await context.OrdenesVenta.Include(ov => ov.OrdenesCompra).SingleAsync(ov => ov.Id == 1);
        Assert.Equal("PorPreparar", order.EstadoLogistico);
        Assert.Empty(order.OrdenesCompra);
    }

    [Fact]
    public async Task Catalogo_PermiteActualizarTarifasYProveedorPrincipal()
    {
        var catalogo = await _client.GetStringAsync("/Catalogo");
        var tokenInput = Regex.Match(catalogo, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        var token = WebUtility.HtmlDecode(Regex.Match(tokenInput, "value=\"([^\"]+)\"").Groups[1].Value);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Id"] = "1",
            ["Nombre"] = "Producto de prueba uno",
            ["Categoria"] = "Seguridad",
            ["Precio"] = "12.50",
            ["tarifasProveedores[0].ProveedorId"] = "1",
            ["tarifasProveedores[0].CostoCompra"] = "11.25",
            ["tarifasProveedores[0].PlazoEntregaHoras"] = "30",
            ["tarifasProveedores[1].ProveedorId"] = "2",
            ["tarifasProveedores[1].CostoCompra"] = "9.50",
            ["tarifasProveedores[1].PlazoEntregaHoras"] = "12",
            ["proveedorPrincipalId"] = "2"
        });

        var response = await _client.PostAsync("/Catalogo/EditarProducto", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var producto = await context.Productos
            .Include(p => p.ProveedoresAutorizados)
            .SingleAsync(p => p.Id == 1);
        Assert.Equal(11.25m, producto.ProveedoresAutorizados.Single(p => p.ProveedorId == 1).CostoCompra);
        Assert.Equal(9.50m, producto.ProveedoresAutorizados.Single(p => p.ProveedorId == 2).CostoCompra);
        Assert.True(producto.ProveedoresAutorizados.Single(p => p.ProveedorId == 2).EsPrincipal);
        Assert.False(producto.ProveedoresAutorizados.Single(p => p.ProveedorId == 1).EsPrincipal);
    }

    private static string[] Pedidos(string html) => Regex.Matches(html, "<tr class=\"order-row\"\\s+data-id=\"([^\"]+)\"")
        .Select(match => match.Groups[1].Value).ToArray();

    private static string Enlace(string html, string atributo, string valor)
    {
        var etiqueta = Regex.Match(html, $"<a\\b[^>]*\\b{Regex.Escape(atributo)}=\"{Regex.Escape(valor)}\"[^>]*>");
        Assert.True(etiqueta.Success, $"No se encontró el enlace {atributo}={valor}");
        var href = Regex.Match(etiqueta.Value, "href=\"([^\"]+)\"");
        Assert.True(href.Success, "El enlace no tiene destino");
        return WebUtility.HtmlDecode(href.Groups[1].Value);
    }

    private async Task SeedPedidosAsync()
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        await context.Database.EnsureCreatedAsync();

        var clientes = new[]
        {
            new Cliente { Ruc = "20492817263", RazonSocial = "Consorcio Vial Andino S.R.C.", DireccionFiscal = "Dirección Consorcio" },
            new Cliente { Ruc = "20601928471", RazonSocial = "Constructora e Ingeniería Minera del Sur S.A.C.", DireccionFiscal = "Dirección Constructora" },
            new Cliente { Ruc = "20554189312", RazonSocial = "Minera del Norte S.A.C.", DireccionFiscal = "Dirección Minera" },
            new Cliente { Ruc = "20112839401", RazonSocial = "Constructora del Pacífico S.A.C.", DireccionFiscal = "Dirección Pacífico" }
        };
        var productoUno = new Producto { Sku = "TEST-01", Nombre = "Producto de prueba uno", UnidadMedida = "UND", CostoReferencial = 12.50m };
        var productoDos = new Producto { Sku = "TEST-02", Nombre = "Producto de prueba dos", UnidadMedida = "PAR", CostoReferencial = 18m };
        var proveedorUno = new Proveedor { Ruc = "20111111111", RazonSocial = "Proveedor Uno S.A.C." };
        var proveedorDos = new Proveedor { Ruc = "20222222222", RazonSocial = "Proveedor Dos S.A.C." };
        productoUno.ProveedoresAutorizados.Add(new ProductoProveedor
        {
            Proveedor = proveedorUno,
            CostoCompra = 12.50m,
            EsPrincipal = true,
            PlazoEntregaHoras = 24
        });
        productoUno.ProveedoresAutorizados.Add(new ProductoProveedor
        {
            Proveedor = proveedorDos,
            CostoCompra = 10.25m,
            PlazoEntregaHoras = 12
        });
        productoDos.ProveedoresAutorizados.Add(new ProductoProveedor
        {
            Proveedor = proveedorUno,
            CostoCompra = 18m,
            EsPrincipal = true,
            PlazoEntregaHoras = 24
        });
        productoDos.ProveedoresAutorizados.Add(new ProductoProveedor
        {
            Proveedor = proveedorDos,
            CostoCompra = 22.75m,
            PlazoEntregaHoras = 12
        });
        context.AddRange(clientes);
        context.AddRange(productoUno, productoDos);

        var pedidos = new[]
        {
            (Sufijo: "0842", Cliente: 0, Estado: "PorPreparar", OrdenCompra: "OC-77821"),
            (Sufijo: "0839", Cliente: 1, Estado: "EnPreparacion", OrdenCompra: "OC-77810"),
            (Sufijo: "0831", Cliente: 2, Estado: "EnPreparacion", OrdenCompra: "OC-77785"),
            (Sufijo: "0824", Cliente: 3, Estado: "Despachado", OrdenCompra: "OC-77742"),
            (Sufijo: "0845", Cliente: 1, Estado: "PorPreparar", OrdenCompra: "OC-77830"),
            (Sufijo: "0835", Cliente: 2, Estado: "EnPreparacion", OrdenCompra: "OC-77790"),
            (Sufijo: "0820", Cliente: 3, Estado: "Despachado", OrdenCompra: "OC-77730"),
            (Sufijo: "0848", Cliente: 0, Estado: "PorPreparar", OrdenCompra: "OC-77850")
        };
        var fechaBase = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < pedidos.Length; index++)
        {
            var item = pedidos[index];
            var cotizacion = new Cotizacion
            {
                Correlativo = $"COT-2026-{item.Sufijo}",
                Cliente = clientes[item.Cliente],
                FechaEmision = fechaBase.AddHours(index),
                Estado = "Aprobada",
                NumeroOrdenCompraCliente = item.OrdenCompra,
                Detalles = new List<CotizacionDetalle>
                {
                    new()
                    {
                        Producto = productoUno,
                        Cantidad = 2,
                        CostoProveedorReferencial = 12.50m,
                        MargenDeseado = 0.2m,
                        PrecioVentaCalculado = 15.63m,
                        Subtotal = 31.26m
                    },
                    new()
                    {
                        Producto = productoDos,
                        Cantidad = 3,
                        CostoProveedorReferencial = 18m,
                        MargenDeseado = 0.2m,
                        PrecioVentaCalculado = 22.50m,
                        Subtotal = 67.50m
                    }
                }
            };
            context.OrdenesVenta.Add(new OrdenVenta
            {
                Cotizacion = cotizacion,
                FechaCreacion = fechaBase.AddHours(index),
                EstadoLogistico = item.Estado
            });
        }

        await context.SaveChangesAsync();
    }

}
