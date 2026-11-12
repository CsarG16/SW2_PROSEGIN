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
using Prosegin.Web.Services.Supabase;
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
        builder.Services.AddHttpClient("ComprobantesClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddDbContext<ProseginDbContext>(options =>
            options.UseSqlite($"Data Source={_database};Pooling=False"));
        builder.Services.AddScoped<IPedidosService, PedidosService>();
        builder.Services.AddSingleton<ISupabaseStorageService, CotizacionesTestHost.SupabaseStoragePrueba>();
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

    [Fact]
    public async Task Seguimiento_FiltraPedidosYValidaElTermino()
    {
        var pedidoFiltrado = await _client.GetStringAsync("/Pedidos?termino=COT-2026-0839");
        var sinCoincidencias = WebUtility.HtmlDecode(await _client.GetStringAsync("/Pedidos?termino=PV-999999"));
        var caracteresInvalidos = WebUtility.HtmlDecode(await _client.GetStringAsync($"/Pedidos?termino={Uri.EscapeDataString("@@##%%")}"));

        Assert.Equal(new[] { "COT-2026-0839" }, Pedidos(pedidoFiltrado));
        Assert.Contains("No se encontraron pedidos que coincidan con la búsqueda", sinCoincidencias);
        Assert.Contains("Ingrese un número de pedido o cliente válido", caracteresInvalidos);
        Assert.DoesNotContain("Actualizar estado", pedidoFiltrado);
        Assert.DoesNotContain("updateStatusDialog", pedidoFiltrado);
    }

    [Fact]
    public async Task Seguimiento_NoExponeEndpointManualActualizarEstado()
    {
        var html = await _client.GetStringAsync("/Pedidos");
        Assert.DoesNotContain("updateStatusDialog", html);
        Assert.Contains("modalPhaseStepper", html);
        Assert.Contains("modalBlockerAlert", html);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["numeroPedido"] = "COT-2026-0842",
            ["nuevoEstado"] = "LISTO_DESPACHO"
        });
        var response = await _client.PostAsync("/Pedidos/ActualizarEstado", form);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var orden = await context.OrdenesVenta.SingleAsync(item => item.Cotizacion.Correlativo == "COT-2026-0842");
        Assert.Equal("PorPreparar", orden.EstadoLogistico);
    }

    [Fact]
    public async Task DetallePedido_ValidaSunatEnTransporte_YRespetaSoloLecturaEnEntregado()
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var enRutaSinSunat = await context.OrdenesVenta
            .Include(ov => ov.Cotizacion)
                .ThenInclude(c => c.Cliente)
            .SingleAsync(item => item.Cotizacion.Correlativo == "COT-2026-0839");
        enRutaSinSunat.EstadoLogistico = "EnRuta";
        enRutaSinSunat.Cotizacion.Cliente.EstadoSunat = "BAJA DEFINITIVA";

        var entregado = await context.OrdenesVenta
            .SingleAsync(item => item.Cotizacion.Correlativo == "COT-2026-0824");
        entregado.EstadoLogistico = "Entregado";
        await context.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IPedidosService>();
        var detalleEnRutaBloqueado = await service.ObtenerDetallePedidoAsync("COT-2026-0839");
        var detalleEntregado = await service.ObtenerDetallePedidoAsync("COT-2026-0824");

        Assert.NotNull(detalleEnRutaBloqueado);
        Assert.Equal("EN_RUTA", detalleEnRutaBloqueado.EstadoOperativo);
        Assert.Equal(3, detalleEnRutaBloqueado.FaseActualOrden);
        Assert.False(string.IsNullOrWhiteSpace(detalleEnRutaBloqueado.BloqueoOperativo));
        Assert.Contains("SUNAT", detalleEnRutaBloqueado.BloqueoOperativo, StringComparison.OrdinalIgnoreCase);
        Assert.False(detalleEnRutaBloqueado.PuedeGenerarFactura);

        Assert.NotNull(detalleEntregado);
        Assert.Equal("ENTREGADO", detalleEntregado.EstadoOperativo);
        Assert.Equal(4, detalleEntregado.FaseActualOrden);
        Assert.Null(detalleEntregado.BloqueoOperativo);
        Assert.False(detalleEntregado.PuedeGenerarOrdenesCompra);
        Assert.False(detalleEntregado.PuedeGenerarGuiaRemision);
        Assert.False(detalleEntregado.PuedeGenerarFactura);
        Assert.False(detalleEntregado.PuedeCancelar);
    }

    [Fact]
    public async Task CancelarPedido_ValidaMotivoGuardaCancelacionYNoPermiteRepetirla()
    {
        using var scope = _app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPedidosService>();

        var vacio = await service.CancelarPedidoAsync("COT-2026-0842", "   ");
        var corto = await service.CancelarPedidoAsync("COT-2026-0842", "ok");
        var cancelacion = await service.CancelarPedidoAsync("COT-2026-0842", "Cliente solicitó anular la compra");
        var repetida = await service.CancelarPedidoAsync("COT-2026-0842", "Cliente solicitó anular la compra");

        Assert.Equal("Ingrese el motivo de la cancelación", vacio.Mensaje);
        Assert.Equal("El motivo debe tener al menos 10 caracteres", corto.Mensaje);
        Assert.True(cancelacion.Exito);
        Assert.Equal("Pedido cancelado correctamente", cancelacion.Mensaje);
        Assert.Equal("El pedido ya se encuentra cancelado", repetida.Mensaje);

        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var orden = await context.OrdenesVenta.SingleAsync(item => item.Cotizacion.Correlativo == "COT-2026-0842");
        Assert.Equal("Cancelado", orden.EstadoLogistico);
        Assert.Equal("Cliente solicitó anular la compra", orden.MotivoCancelacion);
    }

    [Fact]
    public async Task CancelarPedido_RechazaPedidosDespachadosYFacturados()
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var enRuta = await context.OrdenesVenta
            .SingleAsync(item => item.Cotizacion.Correlativo == "COT-2026-0839");
        enRuta.EstadoLogistico = "EnRuta";
        var facturado = await context.OrdenesVenta
            .Include(item => item.Cotizacion)
            .ThenInclude(cotizacion => cotizacion.Cliente)
            .SingleAsync(item => item.Cotizacion.Correlativo == "COT-2026-0831");
        facturado.EstadoLogistico = "Entregado";
        context.Facturas.Add(new Factura
        {
            OrdenVentaId = facturado.Id,
            ClienteId = facturado.Cotizacion.ClienteId,
            NumeroFactura = "F001-000130",
            Serie = "F001",
            Numero = 130,
            FechaEmision = DateTime.Today,
            FechaVencimiento = DateTime.Today.AddDays(30),
            EstadoPago = "Pendiente",
            EstadoEmision = "Aceptado",
            Total = facturado.Cotizacion.Total
        });
        await context.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IPedidosService>();
        var resultadoEnRuta = await service.CancelarPedidoAsync("COT-2026-0839", "Error en el pedido");
        var resultadoDespachado = await service.CancelarPedidoAsync("COT-2026-0824", "Error en el pedido");
        var resultadoFacturado = await service.CancelarPedidoAsync("COT-2026-0831", "Error en el pedido");

        Assert.Equal("El pedido ya fue despachado y no admite cancelación", resultadoEnRuta.Mensaje);
        Assert.Equal("El pedido ya fue despachado y no admite cancelación", resultadoDespachado.Mensaje);
        Assert.Equal("El pedido ya fue facturado y no admite cancelación", resultadoFacturado.Mensaje);
        Assert.Equal("EnRuta", enRuta.EstadoLogistico);
        Assert.Equal("Entregado", facturado.EstadoLogistico);
    }

    [Fact]
    public async Task CancelarPedido_PostConfirmaCambioYVolverDejaElPedidoIntacto()
    {
        var html = await _client.GetStringAsync("/Pedidos");
        Assert.Contains("Cancelar pedido de venta", html);
        Assert.Contains("Volver", html);
        Assert.Contains("order-dialog-header", html);
        Assert.Contains("btn-prosegin-danger", html);
        var tokenInput = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        var token = WebUtility.HtmlDecode(Regex.Match(tokenInput, "value=\"([^\"]+)\"").Groups[1].Value);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["numeroPedido"] = "COT-2026-0842",
            ["motivo"] = "Cliente solicitó anular la compra",
            ["estado"] = "ALL",
            ["pagina"] = "1"
        });

        var response = await _client.PostAsync("/Pedidos/CancelarPedido", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Pedido cancelado correctamente", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Contains("Cancelado", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var orden = await context.OrdenesVenta.SingleAsync(item => item.Cotizacion.Correlativo == "COT-2026-0842");
        Assert.Equal("Cancelado", orden.EstadoLogistico);
        Assert.Equal("Cliente solicitó anular la compra", orden.MotivoCancelacion);
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
        Assert.Equal(1, order.Revision);
        Assert.Equal(2, order.OrdenesCompra.Count);
        Assert.Equal(25.00m, order.OrdenesCompra.Single(oc => oc.ProveedorId == 1).Total);
        Assert.Equal(68.25m, order.OrdenesCompra.Single(oc => oc.ProveedorId == 2).Total);
        Assert.Equal(2, order.OrdenesCompra.Sum(oc => oc.Detalles.Count));
    }

    [Fact]
    public async Task GenerarOrdenesCompra_ClaimConcurrente_BloqueaLaSegundaEjecucion()
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();

        var primeraEjecucion = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE OrdenesVenta SET EstadoLogistico = 'ListoParaDespacho', Revision = Revision + 1 WHERE Id = 1 AND EstadoLogistico = 'PorPreparar' AND 0 = (SELECT COUNT(*) FROM OrdenesCompra WHERE OrdenVentaId = 1)");
        var segundaEjecucion = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE OrdenesVenta SET EstadoLogistico = 'ListoParaDespacho', Revision = Revision + 1 WHERE Id = 1 AND EstadoLogistico = 'PorPreparar' AND 0 = (SELECT COUNT(*) FROM OrdenesCompra WHERE OrdenVentaId = 1)");

        Assert.Equal(1, primeraEjecucion);
        Assert.Equal(0, segundaEjecucion);
    }

    [Fact]
    public async Task GenerarOrdenesCompra_RechazaTarifaCambiadaDesdeQueSeAbrioLaPantalla()
    {
        var abastecimientoHtml = await _client.GetStringAsync("/Abastecimiento/Index?id=1");
        var tokenInput = Regex.Match(abastecimientoHtml, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        var token = WebUtility.HtmlDecode(Regex.Match(tokenInput, "value=\"([^\"]+)\"").Groups[1].Value);

        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
            var tarifa = await context.ProductoProveedores.SingleAsync(pp => pp.ProductoId == 1 && pp.ProveedorId == 1);
            tarifa.CostoCompra = 15.00m;
            await context.SaveChangesAsync();
        }

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ordenVentaId"] = "1",
            ["selecciones[0].ProductoId"] = "1",
            ["selecciones[0].ProveedorId"] = "1",
            ["selecciones[0].CostoCompra"] = "12.50",
            ["selecciones[1].ProductoId"] = "2",
            ["selecciones[1].ProveedorId"] = "2",
            ["selecciones[1].CostoCompra"] = "22.75"
        });

        var response = await _client.PostAsync("/Abastecimiento/GenerarOrdenesCompra", form);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La tarifa de", payload);
        using var scopeAfter = _app.Services.CreateScope();
        var contextAfter = scopeAfter.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var order = await contextAfter.OrdenesVenta.Include(ov => ov.OrdenesCompra).SingleAsync(ov => ov.Id == 1);
        Assert.Equal("PorPreparar", order.EstadoLogistico);
        Assert.Empty(order.OrdenesCompra);
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
