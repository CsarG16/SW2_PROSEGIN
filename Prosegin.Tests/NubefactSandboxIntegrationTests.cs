using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prosegin.Web.Services.Nubefact;
using Prosegin.Web.Services.Nubefact.DTOs;
using Xunit;
using Xunit.Abstractions;

namespace Prosegin.Tests;

/// <summary>
/// Prueba de integración real contra el entorno Sandbox de Nubefact.
/// Permite verificar de punta a punta la comunicación, serialización y respuesta de Nubefact
/// sin necesidad de implementar vistas o flujos de usuario todavía.
/// </summary>
public class NubefactSandboxIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public NubefactSandboxIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Prueba de integración en vivo contra la Sandbox real de Nubefact. Quitar Skip para lanzar una emisión real a Nubefact.")]
    [Trait("Category", "Integration")]
    public async Task EnviarFacturaReal_ContraNubefactSandbox_EmiteComprobanteAceptado()
    {
        // 1. Configuración real de Sandbox
        var settings = Options.Create(new NubefactSettings
        {
            BaseUrl = "https://api.nubefact.com/api/v1/9b9b7084-b22a-4ef9-a61b-5432a6fe1b13",
            Token = "4020bb6d76c349d89edc7c4339bc92f15dd0f689d0dd4b58bee0d6d86771efde",
            TimeoutSeconds = 15
        });

        // 2. HttpClient real (sin mocks)
        using var httpClient = new HttpClient();
        var service = new NubefactService(httpClient, settings, NullLogger<NubefactService>.Instance);

        // 3. Payload de Factura de prueba con RUC válido para SUNAT/Nubefact
        // En Nubefact Sandbox, el correlativo debe ser el siguiente consecutivo (actualmente 35263)
        var request = new NubefactFacturaRequest
        {
            Serie = "FFF1",
            Numero = 35264,
            ClienteTipoDeDocumento = "6", // RUC
            ClienteNumeroDeDocumento = "20123456786", // RUC con dígito verificador válido
            ClienteDenominacion = "PROSEGIN PRUEBAS INTEGRACION SAC",
            ClienteDireccion = "Av. Argentina 3088, Lima",
            TotalGravada = 100.00m,
            TotalIgv = 18.00m,
            Total = 118.00m,
            OrdenCompraServicio = "COT-2026-TEST",
            Items = new List<NubefactFacturaItem>
            {
                new()
                {
                    UnidadDeMedida = "NIU",
                    Codigo = "EPP-CAS-01",
                    Descripcion = "Casco de Seguridad MSA V-Gard Blanco 3\"", // Con comillas para validar sanitización
                    Cantidad = 2,
                    ValorUnitario = 50.00m,
                    PrecioUnitario = 59.00m,
                    Subtotal = 100.00m,
                    TipoDeIgv = 1, // Gravado
                    Igv = 18.00m,
                    Total = 118.00m,
                    AnticipoRegularizacion = false
                }
            }
        };

        // 4. Enviar mediante el servicio C#
        var resultado = await service.EnviarFacturaAsync(request);

        // 5. Imprimir los resultados reales de Nubefact
        _output.WriteLine($"=== RESPUESTA REAL DE NUBEFACT SANDBOX ===");
        _output.WriteLine($"Éxito: {resultado.Exito}");
        _output.WriteLine($"Estado: {resultado.EstadoProceso}");
        _output.WriteLine($"Descripción SUNAT: {resultado.SunatDescription}");
        _output.WriteLine($"Hash: {resultado.CodigoHash}");
        _output.WriteLine($"Enlace PDF: {resultado.EnlacePdf}");
        _output.WriteLine($"Enlace XML: {resultado.EnlaceXml}");
        _output.WriteLine($"Enlace CDR: {resultado.EnlaceCdr}");
        _output.WriteLine($"Mensaje Error: {resultado.MensajeError}");

        // 6. Aserciones
        Assert.True(resultado.Exito, $"Falló el envío a Nubefact: {resultado.MensajeError}");
        Assert.Equal(NubefactService.EstadoAceptado, resultado.EstadoProceso);
        Assert.NotNull(resultado.CodigoHash);
        Assert.NotNull(resultado.EnlacePdf);
        Assert.Contains(".pdf", resultado.EnlacePdf);
    }
}
