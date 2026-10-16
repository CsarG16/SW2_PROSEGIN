using System.Text.Json.Serialization;

namespace Prosegin.Web.Services.Nubefact.DTOs;

/// <summary>
/// DTO de solicitud de Comprobante de Pago Electrónico (Factura / Boleta) según el estándar oficial JSON de Nubefact.
/// </summary>
public class NubefactFacturaRequest
{
    [JsonPropertyName("operacion")]
    public string Operacion { get; set; } = "generar_comprobante";

    [JsonPropertyName("tipo_de_comprobante")]
    public int TipoDeComprobante { get; set; } = 1; // 1 = Factura, 2 = Boleta

    [JsonPropertyName("serie")]
    public string Serie { get; set; } = string.Empty;

    [JsonPropertyName("numero")]
    public int Numero { get; set; }

    [JsonPropertyName("sunat_transaction")]
    public int SunatTransaction { get; set; } = 1; // 1 = Venta interna

    [JsonPropertyName("cliente_tipo_de_documento")]
    public string ClienteTipoDeDocumento { get; set; } = "6"; // 6 = RUC, 1 = DNI

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

    [JsonPropertyName("fecha_de_vencimiento")]
    public string? FechaDeVencimiento { get; set; }

    /// <summary>
    /// Moneda como entero según Nubefact: 1 = Soles (PEN), 2 = Dólares (USD).
    /// </summary>
    [JsonPropertyName("moneda")]
    public int Moneda { get; set; } = 1;

    [JsonPropertyName("tipo_de_cambio")]
    public decimal? TipoDeCambio { get; set; }

    [JsonPropertyName("porcentaje_de_igv")]
    public decimal PorcentajeDeIgv { get; set; } = 18.00m;

    [JsonPropertyName("total_gravada")]
    public decimal TotalGravada { get; set; }

    [JsonPropertyName("total_inafecta")]
    public decimal? TotalInafecta { get; set; }

    [JsonPropertyName("total_exonerada")]
    public decimal? TotalExonerada { get; set; }

    [JsonPropertyName("total_igv")]
    public decimal TotalIgv { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("orden_compra_servicio")]
    public string? OrdenCompraServicio { get; set; }

    [JsonPropertyName("enviar_automaticamente_a_la_sunat")]
    public bool EnviarAutomaticamenteALaSunat { get; set; } = true;

    [JsonPropertyName("enviar_automaticamente_al_cliente")]
    public bool EnviarAutomaticamenteAlCliente { get; set; } = false;

    [JsonPropertyName("condiciones_de_pago")]
    public string? CondicionesDePago { get; set; }

    [JsonPropertyName("observaciones")]
    public string? Observaciones { get; set; }

    [JsonPropertyName("items")]
    public List<NubefactFacturaItem> Items { get; set; } = new();

    [JsonPropertyName("guias")]
    public List<NubefactGuiaRelacionada>? Guias { get; set; }
}

public class NubefactFacturaItem
{
    [JsonPropertyName("unidad_de_medida")]
    public string UnidadDeMedida { get; set; } = "NIU"; // NIU = Productos/EPPs, ZZ = Servicios

    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("cantidad")]
    public decimal Cantidad { get; set; }

    /// <summary>
    /// Valor Unitario: Precio de venta unitario SIN IGV (admite hasta 10 decimales).
    /// </summary>
    [JsonPropertyName("valor_unitario")]
    public decimal ValorUnitario { get; set; }

    /// <summary>
    /// Precio Unitario: Precio de venta unitario CON IGV (admite hasta 10 decimales).
    /// </summary>
    [JsonPropertyName("precio_unitario")]
    public decimal PrecioUnitario { get; set; }

    [JsonPropertyName("descuento")]
    public decimal? Descuento { get; set; }

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    /// <summary>
    /// 1 = Gravado - Operación Onerosa (código 10 SUNAT).
    /// </summary>
    [JsonPropertyName("tipo_de_igv")]
    public int TipoDeIgv { get; set; } = 1;

    [JsonPropertyName("igv")]
    public decimal Igv { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("anticipo_regularizacion")]
    public bool AnticipoRegularizacion { get; set; } = false;
}

public class NubefactGuiaRelacionada
{
    [JsonPropertyName("guia_tipo")]
    public int GuiaTipo { get; set; } = 1; // 1 = Guía de Remisión Remitente

    [JsonPropertyName("guia_serie_numero")]
    public string GuiaSerieNumero { get; set; } = string.Empty;
}
