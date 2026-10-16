namespace Prosegin.Web.Services.Nubefact.DTOs;

/// <summary>
/// DTO unificado, limpio y tipado expuesto a los desarrolladores de HU 6.1 y HU 6.2.
/// </summary>
public class NubefactResponseDto
{
    /// <summary>
    /// Estados posibles del proceso: "Aceptado", "Rechazado", "Pendiente_Envio"
    /// </summary>
    public string EstadoProceso { get; set; } = "Pendiente_Envio";

    /// <summary>
    /// Indica si el comprobante fue procesado y aceptado con éxito por Nubefact/SUNAT.
    /// </summary>
    public bool Exito => EstadoProceso == "Aceptado";

    /// <summary>
    /// Código Hash generado por SUNAT.
    /// </summary>
    public string? CodigoHash { get; set; }

    /// <summary>
    /// Cadena de texto para renderizar el código QR.
    /// </summary>
    public string? CadenaQr { get; set; }

    /// <summary>
    /// Enlace público de descarga del PDF generado por Nubefact.
    /// </summary>
    public string? EnlacePdf { get; set; }

    /// <summary>
    /// Enlace público de descarga del XML firmado.
    /// </summary>
    public string? EnlaceXml { get; set; }

    /// <summary>
    /// Enlace público de descarga de la Constancia de Recepción (CDR) de SUNAT.
    /// </summary>
    public string? EnlaceCdr { get; set; }

    /// <summary>
    /// Mensaje descriptivo de error devuelto por Nubefact o causa técnica (Timeout/Red).
    /// </summary>
    public string? MensajeError { get; set; }

    /// <summary>
    /// Código de error devuelto por Nubefact (10 = Token, 20/21 = Formato, 23 = Ya existe, etc.).
    /// </summary>
    public int? CodigoError { get; set; }

    /// <summary>
    /// Indica si SUNAT dio por aceptado el comprobante de forma directa.
    /// </summary>
    public bool? AceptadaPorSunat { get; set; }

    /// <summary>
    /// Descripción devuelta por SUNAT (ej. "La Factura numero FFF1-1 ha sido aceptada").
    /// </summary>
    public string? SunatDescription { get; set; }

    public int? TipoDeComprobante { get; set; }
    public string? Serie { get; set; }
    public int? Numero { get; set; }
}
