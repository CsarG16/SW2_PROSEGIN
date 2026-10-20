using Prosegin.Web.Services.Nubefact.DTOs;

namespace Prosegin.Web.Services.Nubefact;

/// <summary>
/// Contrato de servicio para la emisión de comprobantes electrónicos mediante la API REST de Nubefact (HT-03).
/// </summary>
public interface INubefactService
{
    /// <summary>
    /// Envía una Factura o Boleta Electrónica a Nubefact (HU 6.1).
    /// </summary>
    Task<NubefactResponseDto> EnviarFacturaAsync(NubefactFacturaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Envía una Guía de Remisión Remitente a Nubefact (HU 6.2).
    /// </summary>
    Task<NubefactResponseDto> EnviarGuiaRemisionAsync(NubefactGuiaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Consulta el estado y los enlaces de descarga oficiales (PDF, XML, CDR) de una Guía de Remisión emitida a SUNAT.
    /// </summary>
    Task<NubefactResponseDto> ConsultarGuiaRemisionAsync(string serie, int numero, CancellationToken ct = default);
}
