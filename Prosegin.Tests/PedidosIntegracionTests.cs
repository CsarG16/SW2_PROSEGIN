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
    private readonly RelojPrueba _reloj = new();

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
        builder.Services.AddSingleton<TimeProvider>(_reloj);
        builder.Services.AddScoped(_ => new ProseginDbContext(new DbContextOptionsBuilder<ProseginDbContext>().Options));
        builder.Services.AddScoped<IPedidosService, PedidosService>();
        _app = builder.Build();
        _app.MapControllerRoute("default", "{controller=Pedidos}/{action=Index}/{id?}");
        await _app.StartAsync();
        var direcciones = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        _client = new HttpClient { BaseAddress = new Uri(direcciones.Addresses.Single()) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
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
        var primera = await _client.GetStringAsync("/Pedidos?termino=PED-2026&estado=ALL");
        var enlace = Enlace(primera, "aria-label", "Página siguiente");
        Assert.Contains("termino=PED-2026", enlace);
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
        Assert.Equal(new[] { "PED-2026-0839" }, Pedidos(filtrada));
    }

    [Theory]
    [InlineData(-3, "0842")]
    [InlineData(99, "0835")]
    public async Task Paginacion_FueraDeRango_UsaUnaPaginaValida(int pagina, string primerPedido)
    {
        var html = await _client.GetStringAsync($"/Pedidos?pagina={pagina}");
        Assert.EndsWith(primerPedido, Pedidos(html)[0]);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(10, 2)]
    [InlineData(18, 1)]
    public async Task Urgencias_SeRecalculanConHoraDeLima_YExcluyenVencidos(int horaLima, int urgentes)
    {
        _reloj.Ahora = new DateTimeOffset(2026, 10, 2, horaLima, 0, 0, TimeSpan.FromHours(-5)).ToUniversalTime();
        var html = await _client.GetStringAsync("/Pedidos");
        Assert.Equal(urgentes, Regex.Matches(html, "Entrega &le; 24h").Count);
        Assert.Matches($"id=\"kpiUrgentText\">\\s*{urgentes} despachos", html);

        var detalle = await _client.GetStringAsync("/Pedidos/Detalle/PED-2026-0842");
        Assert.Contains($"\"esUrgenteMenor24h\":{(horaLima <= 16 ? "true" : "false")}", detalle);
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

    private sealed class RelojPrueba : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
