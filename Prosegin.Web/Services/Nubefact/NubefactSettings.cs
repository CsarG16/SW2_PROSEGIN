namespace Prosegin.Web.Services.Nubefact;

public class NubefactSettings
{
    public const string SectionName = "NubefactSettings";

    public string BaseUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Serie por defecto para Facturas (en Sandbox: FFF1; en Producción SUNAT: F001).
    /// </summary>
    public string SerieFactura { get; set; } = "FFF1";

    /// <summary>
    /// Serie por defecto para Guías Remitente (en Sandbox: TTT1; en Producción SUNAT: T001).
    /// Según el estándar de SUNAT (Catálogo 51), la serie debe tener obligatoriamente 4 caracteres exactos.
    /// </summary>
    public string SerieGuia { get; set; } = "TTT1";

    /// <summary>
    /// Correlativo inicial para Facturas cuando no existen previas (en Producción 1; en Sandbox compartido se sobreescribe desde appsettings).
    /// </summary>
    public int CorrelativoInicialFactura { get; set; } = 1;

    /// <summary>
    /// Correlativo inicial para Guías cuando no existen previas (en Producción 1; en Sandbox compartido se sobreescribe desde appsettings).
    /// </summary>
    public int CorrelativoInicialGuia { get; set; } = 1;
}
