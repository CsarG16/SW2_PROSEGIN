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
}
