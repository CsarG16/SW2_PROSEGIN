namespace Prosegin.Web.Services
{
    public record SunatConsultaResult(
        bool Success,
        string? Message,
        string? Ruc,
        string? RazonSocial,
        string? DireccionFiscal,
        string? Departamento,
        string? Provincia,
        string? Distrito,
        string? Ubigeo,
        string? Estado,
        string? Condicion,
        string? TipoEmision,
        string? Fuente,
        bool EsHabido,
        bool EsActivo
    );

    public interface ISunatService
    {
        Task<SunatConsultaResult> ConsultarRucAsync(string ruc, CancellationToken cancellationToken = default);
        bool ValidarFormatoRuc(string ruc, out string? mensajeError);
    }
}
