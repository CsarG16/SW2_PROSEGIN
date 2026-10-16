using System.Text.Json.Serialization;

namespace Prosegin.Web.Services.Nubefact.DTOs;

/// <summary>
/// DTO de solicitud de Guía de Remisión Remitente según el estándar oficial JSON de Nubefact.
/// </summary>
public class NubefactGuiaRequest
{
    [JsonPropertyName("operacion")]
    public string Operacion { get; set; } = "generar_guia";

    [JsonPropertyName("tipo_de_comprobante")]
    public int TipoDeComprobante { get; set; } = 7; // 7 = Guía de Remisión Remitente

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("numero")]
    public int Numero { get; set; }

    [JsonPropertyName("cliente_tipo_de_documento")]
    public string ClienteTipoDeDocumento { get; set; } = "6";

    [JsonPropertyName("cliente_numero_de_documento")]
    public string ClienteNumeroDeDocumento { get; set; } = string.Empty;

    [JsonPropertyName("cliente_denominacion")]
    public string ClienteDenominacion { get; set; } = string.Empty;

    [JsonPropertyName("cliente_direccion")]
    public string ClienteDireccion { get; set; } = string.Empty;

    [JsonPropertyName("cliente_email")]
    public string? ClienteEmail { get; set; }

    [JsonPropertyName("fecha_de_emision")]
    public string FechaDeEmision { get; set; } = DateTime.Now.ToString("dd-MM-yyyy");

    [JsonPropertyName("fecha_de_inicio_de_traslado")]
    public string FechaDeInicioDeTraslado { get; set; } = DateTime.Now.ToString("dd-MM-yyyy");

    /// <summary>
    /// 01 = Venta, 14 = Venta con entrega a terceros, etc.
    /// </summary>
    [JsonPropertyName("motivo_de_traslado")]
    public string MotivoDeTraslado { get; set; } = "01";

    [JsonPropertyName("peso_bruto_total")]
    public decimal PesoBrutoTotal { get; set; }

    [JsonPropertyName("peso_bruto_unidad_de_medida")]
    public string PesoBrutoUnidadDeMedida { get; set; } = "KGM";

    [JsonPropertyName("numero_de_bultos")]
    public int NumeroDeBultos { get; set; } = 1;

    /// <summary>
    /// 01 = Transporte Público, 02 = Transporte Privado.
    /// </summary>
    [JsonPropertyName("tipo_de_transporte")]
    public string TipoDeTransporte { get; set; } = "02";

    [JsonPropertyName("punto_de_partida_ubigeo")]
    public string PuntoDePartidaUbigeo { get; set; } = string.Empty;

    [JsonPropertyName("punto_de_partida_direccion")]
    public string PuntoDePartidaDireccion { get; set; } = string.Empty;

    [JsonPropertyName("punto_de_llegada_ubigeo")]
    public string PuntoDeLlegadaUbigeo { get; set; } = string.Empty;

    [JsonPropertyName("punto_de_llegada_direccion")]
    public string PuntoDeLlegadaDireccion { get; set; } = string.Empty;

    [JsonPropertyName("observaciones")]
    public string? Observaciones { get; set; }

    [JsonPropertyName("transportista_documento_tipo")]
    public string? TransportistaDocumentoTipo { get; set; }

    [JsonPropertyName("transportista_documento_numero")]
    public string? TransportistaDocumentoNumero { get; set; }

    [JsonPropertyName("transportista_denominacion")]
    public string? TransportistaDenominacion { get; set; }

    [JsonPropertyName("transportista_placa_numero")]
    public string? TransportistaPlacaNumero { get; set; }

    [JsonPropertyName("conductor_documento_tipo")]
    public string? ConductorDocumentoTipo { get; set; }

    [JsonPropertyName("conductor_documento_numero")]
    public string? ConductorDocumentoNumero { get; set; }

    [JsonPropertyName("conductor_denominacion")]
    public string? ConductorDenominacion { get; set; }

    [JsonPropertyName("conductor_numero_licencia")]
    public string? ConductorNumeroLicencia { get; set; }

    [JsonPropertyName("items")]
    public List<NubefactGuiaItem> Items { get; set; } = new();
}

public class NubefactGuiaItem
{
    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("unidad_de_medida")]
    public string UnidadDeMedida { get; set; } = "NIU";

    [JsonPropertyName("cantidad")]
    public decimal Cantidad { get; set; }
}
