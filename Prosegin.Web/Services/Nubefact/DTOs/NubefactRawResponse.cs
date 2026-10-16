using System.Text.Json.Serialization;

namespace Prosegin.Web.Services.Nubefact.DTOs;

/// <summary>
/// Mapeo interno de la respuesta JSON exitosa (HTTP 200 OK) devuelta por la API de Nubefact.
/// </summary>
public class NubefactRawResponse
{
    [JsonPropertyName("tipo_de_comprobante")]
    public int? TipoDeComprobante { get; set; }

    [JsonPropertyName("serie")]
    public string? Serie { get; set; }

    [JsonPropertyName("numero")]
    public int? Numero { get; set; }

    [JsonPropertyName("enlace")]
    public string? Enlace { get; set; }

    [JsonPropertyName("aceptada_por_sunat")]
    public bool? AceptadaPorSunat { get; set; }

    [JsonPropertyName("sunat_description")]
    public string? SunatDescription { get; set; }

    [JsonPropertyName("sunat_note")]
    public string? SunatNote { get; set; }

    [JsonPropertyName("sunat_responsecode")]
    public string? SunatResponseCode { get; set; }

    [JsonPropertyName("sunat_soap_error")]
    public string? SunatSoapError { get; set; }

    [JsonPropertyName("anulado")]
    public bool? Anulado { get; set; }

    [JsonPropertyName("cadena_para_codigo_qr")]
    public string? CadenaParaCodigoQr { get; set; }

    [JsonPropertyName("codigo_hash")]
    public string? CodigoHash { get; set; }

    [JsonPropertyName("enlace_del_pdf")]
    public string? EnlaceDelPdf { get; set; }

    [JsonPropertyName("enlace_del_xml")]
    public string? EnlaceDelXml { get; set; }

    [JsonPropertyName("enlace_del_cdr")]
    public string? EnlaceDelCdr { get; set; }
}

/// <summary>
/// Mapeo interno de la respuesta de error (HTTP 400 / 401 / 500) devuelta por la API de Nubefact.
/// Estructura estándar: { "errors": "...", "codigo": 20 }
/// </summary>
public class NubefactErrorResponse
{
    [JsonPropertyName("errors")]
    public string? Errors { get; set; }

    [JsonPropertyName("codigo")]
    public int? Codigo { get; set; }
}
