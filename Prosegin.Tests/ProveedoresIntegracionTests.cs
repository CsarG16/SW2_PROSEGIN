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

public sealed class ProveedoresIntegracionTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"prosegin-proveedores-{Guid.NewGuid():N}.db");
    private readonly SunatPrueba _sunat = new();

    public async Task InitializeAsync()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Prosegin.slnx")))
        {
            root = root.Parent;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ProveedoresController).Assembly.GetName().Name,
            ContentRootPath = Path.Combine(root!.FullName, "Prosegin.Web"),
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ProveedoresController).Assembly);
        builder.Services.AddDbContext<ProseginDbContext>(options =>
            options.UseSqlite($"Data Source={_database};Pooling=False"));
        builder.Services.AddSingleton<ISunatService>(_sunat);
        _app = builder.Build();
        _app.MapControllerRoute("default", "{controller=Proveedores}/{action=Index}/{id?}");

        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
            await context.Database.EnsureCreatedAsync();
            context.Proveedores.AddRange(
                new Proveedor
                {
                    Ruc = "10100000003",
                    RazonSocial = "Proveedor Activo S.A.C.",
                    NombreComercial = "Marca Activa",
                    Telefono = "900111222",
                    CorreoComercial = "ventas@activo.test"
                },
                new Proveedor
                {
                    Ruc = "20100000001",
                    RazonSocial = "Proveedor Inactivo S.A.C.",
                    Activo = false
                });
            await context.SaveChangesAsync();
        }

        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(address)
        };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        File.Delete(_database);
    }

    [Fact]
    public async Task Directorio_BuscaPorNombreYMuestraProveedoresInactivos()
    {
        var response = await _client.GetAsync("/Proveedores?termino=inactivo");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Proveedor Inactivo S.A.C.", html);
        Assert.Contains("Inactivo", html);
        Assert.DoesNotContain("Proveedor Activo S.A.C.", html);
    }

    [Fact]
    public async Task Directorio_BuscaPorNombreComercial()
    {
        var response = await _client.GetAsync("/Proveedores?termino=Marca%20Activa");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Proveedor Activo S.A.C.", html);
        Assert.Contains("Marca Activa", html);
        Assert.DoesNotContain("Proveedor Inactivo S.A.C.", html);
    }

    [Fact]
    public async Task Directorio_PaginaResultadosYRespetaElTamanoSolicitado()
    {
        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
            for (var i = 0; i < 21; i++)
            {
                context.Proveedores.Add(new Proveedor
                {
                    Ruc = (20200000000L + i).ToString(),
                    RazonSocial = $"Paginacion {i:D2}"
                });
            }

            await context.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/Proveedores?pagina=2&tamanoPagina=10");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Mostrando 11 - 20 de 23 registros", html);
        Assert.Contains("Paginacion 10", html);
        Assert.DoesNotContain("Paginacion 09", html);
    }

    [Fact]
    public async Task Formulario_ExponeLasSeccionesYCamposDeLaGui()
    {
        var html = await _client.GetStringAsync("/Proveedores/Create");

        Assert.Contains("Registrar Proveedor de EPP", html);
        Assert.Contains("DATOS DE LA EMPRESA", html);
        Assert.Contains("PERSONA DE CONTACTO COMERCIAL / VENTAS", html);
        Assert.Contains("INFORMACIÓN DE PAGO (CUENTAS BANCARIAS)", html);
        Assert.Contains("Especialidad / Líneas de EPP Suministradas", html);
        Assert.Contains("Condición de Crédito / Pago Habitual", html);
        Assert.Contains("ESTADO TRIBUTARIO", html);
        Assert.Contains("USD", html);
    }

    [Fact]
    public async Task RegistrarProveedor_ConsultaSunatYGuardaContactoYCci()
    {
        var token = await ObtenerTokenAsync("/Proveedores/Create");
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Ruc"] = "20100047218",
            ["RazonSocial"] = "Razón que será reemplazada por SUNAT",
            ["NombreComercial"] = "EMPRESA OFICIAL",
            ["DireccionFiscal"] = "Dirección que será reemplazada por SUNAT",
            ["Especialidad"] = "Protección de Manos",
            ["CondicionPago"] = "Crédito a 30 días",
            ["Telefono"] = "900123456",
            ["CorreoComercial"] = "contacto@empresa.test",
            ["Contactos[0].Nombre"] = "Ana Proveedora",
            ["Contactos[0].Cargo"] = "Ventas",
            ["Contactos[0].Telefono"] = "900123456",
            ["Contactos[0].Correo"] = "ana@empresa.test",
            ["Contactos[0].EsPrincipal"] = "true",
            ["CuentasBancarias[0].Banco"] = "BCP",
            ["CuentasBancarias[0].Moneda"] = "USD",
            ["CuentasBancarias[0].NumeroCuenta"] = "",
            ["CuentasBancarias[0].Cci"] = "00219123456789012345"
        };

        var response = await _client.PostAsync("/Proveedores/Create", new FormUrlEncodedContent(fields));

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var proveedor = await context.Proveedores
            .Include(p => p.Contactos)
            .Include(p => p.CuentasBancarias)
            .SingleAsync(p => p.Ruc == "20100047218");
        Assert.Equal("EMPRESA OFICIAL S.A.C.", proveedor.RazonSocial);
        Assert.Equal("Av. Oficial 123", proveedor.DireccionFiscal);
        Assert.Equal("EMPRESA OFICIAL", proveedor.NombreComercial);
        Assert.Equal("Protección de Manos", proveedor.Especialidad);
        Assert.Equal("Crédito a 30 días", proveedor.CondicionPago);
        Assert.Single(proveedor.Contactos);
        Assert.Equal("Ana Proveedora", proveedor.Contactos.Single().Nombre);
        Assert.Equal("00219123456789012345", proveedor.CuentasBancarias.Single().Cci);
        Assert.Equal("USD", proveedor.CuentasBancarias.Single().Moneda);
    }

    [Fact]
    public async Task RegistrarProveedor_RechazaRucNoActivoEnSunat()
    {
        _sunat.Estado = "BAJA";
        var token = await ObtenerTokenAsync("/Proveedores/Create");
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Ruc"] = "20100047218",
            ["RazonSocial"] = "Empresa",
            ["NombreComercial"] = "Empresa",
            ["DireccionFiscal"] = "Dirección",
            ["Especialidad"] = "Protección de Manos",
            ["CondicionPago"] = "Contado",
            ["Telefono"] = "900123456",
            ["CorreoComercial"] = "contacto@empresa.test",
            ["Contactos[0].Nombre"] = "Ana",
            ["Contactos[0].Cargo"] = "Ventas",
            ["Contactos[0].Telefono"] = "900123456",
            ["Contactos[0].Correo"] = "ana@empresa.test",
            ["CuentasBancarias[0].Banco"] = "BCP",
            ["CuentasBancarias[0].Cci"] = "00219123456789012345"
        };

        var response = await _client.PostAsync("/Proveedores/Create", new FormUrlEncodedContent(fields));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ACTIVO y HABIDO", html);
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        Assert.Equal(2, await context.Proveedores.CountAsync());
    }

    [Fact]
    public async Task InactivarProveedor_ConservaRegistroEnElDirectorio()
    {
        var token = await ObtenerTokenAsync("/Proveedores");
        var response = await _client.PostAsync("/Proveedores/Inactivate/1",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProseginDbContext>();
        var proveedor = await context.Proveedores.SingleAsync(p => p.Id == 1);
        Assert.False(proveedor.Activo);
        var html = await _client.GetStringAsync("/Proveedores?termino=activo");
        Assert.Contains("Proveedor Activo S.A.C.", html);
        Assert.Contains("Inactivo", html);
    }

    private async Task<string> ObtenerTokenAsync(string ruta)
    {
        var html = await _client.GetStringAsync(ruta);
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        Assert.NotEmpty(input);
        return WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value);
    }

    private sealed class SunatPrueba : ISunatService
    {
        public string Estado { get; set; } = "ACTIVO";

        public bool ValidarFormatoRuc(string ruc, out string? mensajeError)
        {
            return Prosegin.Data.Validation.RucValidator.ValidarFormato(ruc, out mensajeError);
        }

        public Task<SunatConsultaResult> ConsultarRucAsync(string ruc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SunatConsultaResult(
                Success: true,
                Message: null,
                Ruc: ruc,
                RazonSocial: "EMPRESA OFICIAL S.A.C.",
                DireccionFiscal: "Av. Oficial 123",
                Departamento: "LIMA",
                Provincia: "LIMA",
                Distrito: "LIMA",
                Ubigeo: "150101",
                Estado: Estado,
                Condicion: "HABIDO",
                RepresentanteLegal: null,
                TipoEmision: "FACTURA",
                Fuente: "Prueba",
                EsHabido: true,
                EsActivo: Estado == "ACTIVO"));
        }
    }
}
