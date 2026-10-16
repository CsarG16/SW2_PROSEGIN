using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Prosegin.Web.Services.Nubefact.DTOs;

namespace Prosegin.Web.Services.Nubefact;

public class NubefactService : INubefactService
{
    private readonly HttpClient _httpClient;
    private readonly NubefactSettings _settings;
    private readonly ILogger<NubefactService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null, // Respeta exactamente [JsonPropertyName]
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public const string EstadoAceptado = "Aceptado";
    public const string EstadoRechazado = "Rechazado";
    public const string EstadoPendienteEnvio = "Pendiente_Envio";

    public NubefactService(
        HttpClient httpClient,
        IOptions<NubefactSettings> options,
        ILogger<NubefactService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigurarHttpClient();
    }

    private void ConfigurarHttpClient()
    {
        if (_httpClient.BaseAddress == null && !string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        }

        // Según el manual oficial de Nubefact, el header Authorization va con el token directo (sin palabra "Bearer")
        if (!string.IsNullOrWhiteSpace(_settings.Token) && !_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", _settings.Token.Trim());
        }

        if (_httpClient.Timeout == TimeSpan.FromSeconds(100) || _httpClient.Timeout.TotalSeconds <= 0)
        {
            var timeoutSec = _settings.TimeoutSeconds > 0 ? _settings.TimeoutSeconds : 10;
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSec);
        }
    }

    public async Task<NubefactResponseDto> EnviarFacturaAsync(
        NubefactFacturaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Si no se especifica serie en el request, usar la configurada en NubefactSettings (FFF1 en Sandbox / F001 en Producción)
        if (string.IsNullOrWhiteSpace(request.Serie))
        {
            request.Serie = !string.IsNullOrWhiteSpace(_settings.SerieFactura) ? _settings.SerieFactura : "FFF1";
        }

        // Limpieza de caracteres de escape comunes que causan error 20/21 en descripciones
        SanitizarFacturaRequest(request);

        return await EnviarComprobanteGenericoAsync(request, "Factura/Boleta", ct);
    }

    public async Task<NubefactResponseDto> EnviarGuiaRemisionAsync(
        NubefactGuiaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Si no se especifica serie en el request, usar la configurada en NubefactSettings (TTT1 en Sandbox / T001 en Producción)
        if (string.IsNullOrWhiteSpace(request.Serie))
        {
            request.Serie = !string.IsNullOrWhiteSpace(_settings.SerieGuia) ? _settings.SerieGuia : "TTT1";
        }

        SanitizarGuiaRequest(request);

        return await EnviarComprobanteGenericoAsync(request, "Guía de Remisión", ct);
    }

    private async Task<NubefactResponseDto> EnviarComprobanteGenericoAsync(
        object requestPayload,
        string tipoOperacion,
        CancellationToken ct)
    {
        try
        {
            var jsonPayload = JsonSerializer.Serialize(requestPayload, JsonOptions);
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Enviando comprobante {TipoOperacion} a Nubefact...", tipoOperacion);

            using var response = await _httpClient.PostAsync("", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            // Caso 1: HTTP 200 OK (Comprobante Aceptado y Procesado)
            if (response.IsSuccessStatusCode)
            {
                var raw = JsonSerializer.Deserialize<NubefactRawResponse>(responseBody, JsonOptions);
                _logger.LogInformation("Comprobante {TipoOperacion} aceptado con éxito en Nubefact. Hash: {Hash}",
                    tipoOperacion, raw?.CodigoHash);

                return new NubefactResponseDto
                {
                    EstadoProceso = EstadoAceptado,
                    CodigoHash = raw?.CodigoHash,
                    CadenaQr = raw?.CadenaParaCodigoQr,
                    EnlacePdf = raw?.EnlaceDelPdf ?? raw?.Enlace,
                    EnlaceXml = raw?.EnlaceDelXml,
                    EnlaceCdr = raw?.EnlaceDelCdr,
                    AceptadaPorSunat = raw?.AceptadaPorSunat,
                    SunatDescription = raw?.SunatDescription,
                    TipoDeComprobante = raw?.TipoDeComprobante,
                    Serie = raw?.Serie,
                    Numero = raw?.Numero,
                    MensajeError = null
                };
            }

            // Caso 2: HTTP 400 Bad Request o 401 Unauthorized (Rechazo de formato o tributario)
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.UnprocessableEntity)
            {
                var errorDto = DeserializarError(responseBody);
                _logger.LogWarning("Comprobante {TipoOperacion} rechazado por Nubefact (HTTP {StatusCode}). Código: {Codigo}, Error: {Error}",
                    tipoOperacion, (int)response.StatusCode, errorDto?.Codigo, errorDto?.Errors);

                return new NubefactResponseDto
                {
                    EstadoProceso = EstadoRechazado,
                    MensajeError = errorDto?.Errors ?? $"Error {(int)response.StatusCode} devuelto por Nubefact.",
                    CodigoError = errorDto?.Codigo
                };
            }

            // Caso 3: HTTP 5xx u otro estado de error del servidor Nubefact
            _logger.LogError("El servidor de Nubefact respondió con error HTTP {StatusCode}: {ResponseBody}",
                (int)response.StatusCode, responseBody);

            return new NubefactResponseDto
            {
                EstadoProceso = EstadoPendienteEnvio,
                MensajeError = $"El servidor de Nubefact respondió con error HTTP {(int)response.StatusCode}. El comprobante queda en estado Pendiente de Envío."
            };
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Timeout del HttpClient (10 segundos superado)
            _logger.LogWarning(ex, "Timeout al comunicarse con Nubefact para {TipoOperacion} (límite de 10s superado).", tipoOperacion);
            return new NubefactResponseDto
            {
                EstadoProceso = EstadoPendienteEnvio,
                MensajeError = "Tiempo de espera agotado al conectar con Nubefact (Timeout de 10s superado). El comprobante queda registrado en estado Pendiente de Envío."
            };
        }
        catch (HttpRequestException ex)
        {
            // Error de conexión a nivel de red, DNS o socket cerrado
            _logger.LogError(ex, "Fallo de conexión o red con la API de Nubefact para {TipoOperacion}.", tipoOperacion);
            return new NubefactResponseDto
            {
                EstadoProceso = EstadoPendienteEnvio,
                MensajeError = $"Fallo de conexión de red con el servicio de Nubefact: {ex.Message}. El comprobante queda registrado en estado Pendiente de Envío."
            };
        }
        catch (Exception ex)
        {
            // Resiliencia total: No rompe el flujo del usuario
            _logger.LogError(ex, "Excepción inesperada al procesar {TipoOperacion} con Nubefact.", tipoOperacion);
            return new NubefactResponseDto
            {
                EstadoProceso = EstadoPendienteEnvio,
                MensajeError = $"Error inesperado al emitir comprobante: {ex.Message}. Registrado como Pendiente de Envío."
            };
        }
    }

    private static NubefactErrorResponse? DeserializarError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;

        try
        {
            return JsonSerializer.Deserialize<NubefactErrorResponse>(responseBody, JsonOptions);
        }
        catch
        {
            return new NubefactErrorResponse { Errors = responseBody };
        }
    }

    private static void SanitizarFacturaRequest(NubefactFacturaRequest request)
    {
        request.ClienteDenominacion = LimpiarTexto(request.ClienteDenominacion);
        request.ClienteDireccion = LimpiarTexto(request.ClienteDireccion);

        if (request.Items != null)
        {
            foreach (var item in request.Items)
            {
                item.Descripcion = LimpiarTexto(item.Descripcion);
                item.Codigo = item.Codigo?.Trim() ?? string.Empty;
            }
        }
    }

    private static void SanitizarGuiaRequest(NubefactGuiaRequest request)
    {
        request.ClienteDenominacion = LimpiarTexto(request.ClienteDenominacion);
        request.ClienteDireccion = LimpiarTexto(request.ClienteDireccion);
        request.PuntoDePartidaDireccion = LimpiarTexto(request.PuntoDePartidaDireccion);
        request.PuntoDeLlegadaDireccion = LimpiarTexto(request.PuntoDeLlegadaDireccion);

        if (request.Items != null)
        {
            foreach (var item in request.Items)
            {
                item.Descripcion = LimpiarTexto(item.Descripcion);
                item.Codigo = item.Codigo?.Trim() ?? string.Empty;
            }
        }
    }

    private static string LimpiarTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        // Reemplazar comillas dobles no escapadas que causan error 20 en el parser de Nubefact
        return texto.Replace("\"", "'").Trim();
    }
}
