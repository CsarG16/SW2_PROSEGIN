using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prosegin.Web.Services.Nubefact;
using Prosegin.Web.Services.Nubefact.DTOs;
using Xunit;

namespace Prosegin.Tests;

public class NubefactServiceTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new TaskCanceledException("HttpClient timeout triggered");
        }
    }

    private class NetworkFailHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Name or service not known");
        }
    }

    private static IOptions<NubefactSettings> CrearOpciones()
    {
        return Options.Create(new NubefactSettings
        {
            BaseUrl = "https://api.nubefact.com/api/v1/sandbox-demo",
            Token = "4020bb6d76c349d89edc7c4339bc92f15dd0f689d0dd4b58bee0d6d86771efde",
            TimeoutSeconds = 10
        });
    }

    [Fact]
    public async Task EnviarFactura_Respuesta200Ok_RetornaEstadoAceptadoYEnlacesCompletos()
    {
        var rawResponseJson = JsonSerializer.Serialize(new
        {
            tipo_de_comprobante = 1,
            serie = "FFF1",
            numero = 1,
            enlace = "https://api.nubefact.com/ver/FFF1-1",
            aceptada_por_sunat = true,
            sunat_description = "La Factura numero FFF1-1 ha sido aceptada",
            cadena_para_codigo_qr = "20601234567|01|FFF1|1|18.00|118.00|15-10-2026|6|20492817263|",
            codigo_hash = "mD1sW+7qFk9kL5k4x=",
            enlace_del_pdf = "https://api.nubefact.com/cpe/FFF1-1.pdf",
            enlace_del_xml = "https://api.nubefact.com/cpe/FFF1-1.xml",
            enlace_del_cdr = "https://api.nubefact.com/cpe/R-FFF1-1.xml"
        });

        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(rawResponseJson)
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new NubefactService(httpClient, CrearOpciones(), NullLogger<NubefactService>.Instance);

        var request = new NubefactFacturaRequest
        {
            Numero = 1,
            ClienteNumeroDeDocumento = "20492817263",
            ClienteDenominacion = "CONSORCIO VIAL ANDINO S.R.C.",
            ClienteDireccion = "Av. Industrial 450",
            TotalGravada = 100.00m,
            TotalIgv = 18.00m,
            Total = 118.00m,
            Items = new List<NubefactFacturaItem>
            {
                new()
                {
                    Codigo = "EPP-CAS-001",
                    Descripcion = "Casco 3M Dieléctrico",
                    Cantidad = 2,
                    ValorUnitario = 50.00m,
                    PrecioUnitario = 59.00m,
                    Subtotal = 100.00m,
                    Igv = 18.00m,
                    Total = 118.00m
                }
            }
        };

        var resultado = await service.EnviarFacturaAsync(request);

        Assert.True(resultado.Exito);
        Assert.Equal(NubefactService.EstadoAceptado, resultado.EstadoProceso);
        Assert.Equal("mD1sW+7qFk9kL5k4x=", resultado.CodigoHash);
        Assert.Equal("https://api.nubefact.com/cpe/FFF1-1.pdf", resultado.EnlacePdf);
        Assert.Equal("https://api.nubefact.com/cpe/FFF1-1.xml", resultado.EnlaceXml);
        Assert.Equal("https://api.nubefact.com/cpe/R-FFF1-1.xml", resultado.EnlaceCdr);
        Assert.Null(resultado.MensajeError);

        // Validar que el Header Authorization no lleva "Bearer" según el manual oficial
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("Authorization"));
        var authHeader = capturedRequest.Headers.GetValues("Authorization").First();
        Assert.Equal("4020bb6d76c349d89edc7c4339bc92f15dd0f689d0dd4b58bee0d6d86771efde", authHeader);
        Assert.DoesNotContain("Bearer", authHeader);
    }

    [Fact]
    public async Task EnviarFactura_Respuesta400BadRequest_RetornaEstadoRechazadoConCodigoYMensaje()
    {
        var errorResponseJson = JsonSerializer.Serialize(new
        {
            errors = "El archivo enviado no cumple con el formato establecido",
            codigo = 20
        });

        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(errorResponseJson)
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new NubefactService(httpClient, CrearOpciones(), NullLogger<NubefactService>.Instance);

        var request = new NubefactFacturaRequest { Numero = 2 };

        var resultado = await service.EnviarFacturaAsync(request);

        Assert.False(resultado.Exito);
        Assert.Equal(NubefactService.EstadoRechazado, resultado.EstadoProceso);
        Assert.Equal("El archivo enviado no cumple con el formato establecido", resultado.MensajeError);
        Assert.Equal(20, resultado.CodigoError);
    }

    [Fact]
    public async Task EnviarFactura_TimeoutDeRed_RetornaEstadoPendienteEnvioSinLanzarExcepcion()
    {
        var httpClient = new HttpClient(new TimeoutHttpMessageHandler());
        var service = new NubefactService(httpClient, CrearOpciones(), NullLogger<NubefactService>.Instance);

        var request = new NubefactFacturaRequest { Numero = 3 };

        var resultado = await service.EnviarFacturaAsync(request);

        Assert.False(resultado.Exito);
        Assert.Equal(NubefactService.EstadoPendienteEnvio, resultado.EstadoProceso);
        Assert.Contains("Timeout", resultado.MensajeError);
    }

    [Fact]
    public async Task EnviarFactura_ErrorDeConexion_RetornaEstadoPendienteEnvioSinLanzarExcepcion()
    {
        var httpClient = new HttpClient(new NetworkFailHttpMessageHandler());
        var service = new NubefactService(httpClient, CrearOpciones(), NullLogger<NubefactService>.Instance);

        var request = new NubefactFacturaRequest { Numero = 4 };

        var resultado = await service.EnviarFacturaAsync(request);

        Assert.False(resultado.Exito);
        Assert.Equal(NubefactService.EstadoPendienteEnvio, resultado.EstadoProceso);
        Assert.Contains("Fallo de conexión", resultado.MensajeError);
    }

    [Fact]
    public async Task EnviarGuiaRemision_Respuesta200Ok_RetornaEstadoAceptado()
    {
        var rawResponseJson = JsonSerializer.Serialize(new
        {
            tipo_de_comprobante = 7,
            serie = "TTT1",
            numero = 10,
            enlace = "https://api.nubefact.com/ver/TTT1-10",
            aceptada_por_sunat = true,
            codigo_hash = "kLmN7890Hash=",
            enlace_del_pdf = "https://api.nubefact.com/cpe/TTT1-10.pdf"
        });

        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(rawResponseJson)
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new NubefactService(httpClient, CrearOpciones(), NullLogger<NubefactService>.Instance);

        var request = new NubefactGuiaRequest
        {
            Numero = 10,
            ClienteNumeroDeDocumento = "20492817263",
            ClienteDenominacion = "CONSORCIO VIAL ANDINO S.R.C.",
            PesoBrutoTotal = 15.5m,
            PuntoDePartidaDireccion = "Av. Argentina 3088",
            PuntoDeLlegadaDireccion = "Av. Industrial 450",
            Items = new List<NubefactGuiaItem>
            {
                new() { Codigo = "EPP-BOT-001", Descripcion = "Botas Bata Titan", Cantidad = 10 }
            }
        };

        var resultado = await service.EnviarGuiaRemisionAsync(request);

        Assert.True(resultado.Exito);
        Assert.Equal(NubefactService.EstadoAceptado, resultado.EstadoProceso);
        Assert.Equal("kLmN7890Hash=", resultado.CodigoHash);
        Assert.Equal("https://api.nubefact.com/cpe/TTT1-10.pdf", resultado.EnlacePdf);
    }

    [Fact]
    public async Task EnviarGuiaRemision_CuandoGenerarGuiaRetornaSoloEnlaceWeb_AseguraExtensionPdf()
    {
        var rawResponseJson = JsonSerializer.Serialize(new
        {
            tipo_de_comprobante = 7,
            serie = "TTT1",
            numero = 11,
            enlace = "https://www.nubefact.com/guia/uuid-12345",
            aceptada_por_sunat = true,
            codigo_hash = "HashTest=",
            enlace_del_pdf = (string?)null
        });

        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(rawResponseJson)
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new NubefactService(httpClient, CrearOpciones(), NullLogger<NubefactService>.Instance);

        var request = new NubefactGuiaRequest
        {
            Numero = 11,
            ClienteNumeroDeDocumento = "20492817263",
            ClienteDenominacion = "CONSORCIO VIAL ANDINO S.R.C.",
            PesoBrutoTotal = 10m,
            PuntoDePartidaDireccion = "Av. Argentina 3088",
            PuntoDeLlegadaDireccion = "Av. Industrial 450",
            Items = new List<NubefactGuiaItem>
            {
                new() { Codigo = "EPP-BOT-001", Descripcion = "Botas Bata Titan", Cantidad = 5 }
            }
        };

        var resultado = await service.EnviarGuiaRemisionAsync(request);

        Assert.True(resultado.Exito);
        Assert.Equal("https://www.nubefact.com/guia/uuid-12345.pdf", resultado.EnlacePdf);
        Assert.Equal("https://www.nubefact.com/guia/uuid-12345.xml", resultado.EnlaceXml);
        Assert.Equal("https://www.nubefact.com/guia/uuid-12345.cdr", resultado.EnlaceCdr);
    }

    [Fact]
    public async Task ConsultarGuiaRemision_Respuesta200Ok_RetornaDatosCompletos()
    {
        var rawResponseJson = JsonSerializer.Serialize(new
        {
            tipo_de_comprobante = 7,
            serie = "TTT1",
            numero = 3,
            enlace = "https://www.nubefact.com/guia/f4027189-005f-4287-aca6-ca5f357b5e53",
            aceptada_por_sunat = true,
            sunat_description = "ACEPTADA",
            cadena_para_codigo_qr = "https://url-test?hashqr=test",
            enlace_del_pdf = "https://www.nubefact.com/guia/f4027189-005f-4287-aca6-ca5f357b5e53.pdf",
            enlace_del_xml = "https://www.nubefact.com/guia/f4027189-005f-4287-aca6-ca5f357b5e53.xml",
            enlace_del_cdr = "https://www.nubefact.com/guia/f4027189-005f-4287-aca6-ca5f357b5e53.cdr"
        });

        var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(rawResponseJson)
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new NubefactService(httpClient, CrearOpciones(), NullLogger<NubefactService>.Instance);

        var resultado = await service.ConsultarGuiaRemisionAsync("TTT1", 3);

        Assert.True(resultado.Exito);
        Assert.Equal(NubefactService.EstadoAceptado, resultado.EstadoProceso);
        Assert.Equal("https://www.nubefact.com/guia/f4027189-005f-4287-aca6-ca5f357b5e53.pdf", resultado.EnlacePdf);
        Assert.Equal("https://www.nubefact.com/guia/f4027189-005f-4287-aca6-ca5f357b5e53.xml", resultado.EnlaceXml);
        Assert.Equal("https://www.nubefact.com/guia/f4027189-005f-4287-aca6-ca5f357b5e53.cdr", resultado.EnlaceCdr);
    }
}

