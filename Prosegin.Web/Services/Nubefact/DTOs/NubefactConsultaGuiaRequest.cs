using System.Text.Json.Serialization;

namespace Prosegin.Web.Services.Nubefact.DTOs;

/// <summary>
/// DTO de consulta de Guía de Remisión Remitente a Nubefact (operación: consultar_guia).
/// Permite recuperar los enlaces oficiales del PDF, XML y CDR validados ante SUNAT.
/// </summary>
public class NubefactConsultaGuiaRequest
{
    [JsonPropertyName("operacion")]
    public string Operacion { get; set; } = "consultar_guia";

    [JsonPropertyName("tipo_de_comprobante")]
    public int TipoDeComprobante { get; set; } = 7; // 7 = Guía de Remisión Remitente

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("numero")]
    public int Numero { get; set; }
}
