using System.Text.Json;
using Prosegin.Data.Validation;

namespace Prosegin.Web.Services
{
    public class SunatService : ISunatService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SunatService> _logger;

        public SunatService(IHttpClientFactory httpClientFactory, ILogger<SunatService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public bool ValidarFormatoRuc(string ruc, out string? mensajeError)
        {
            return RucValidator.ValidarFormato(ruc, out mensajeError);
        }

        public async Task<SunatConsultaResult> ConsultarRucAsync(string ruc, CancellationToken cancellationToken = default)
        {
            ruc = ruc?.Trim() ?? string.Empty;

            if (!ValidarFormatoRuc(ruc, out var errorFormato))
            {
                return new SunatConsultaResult(
                    Success: false,
                    Message: errorFormato,
                    Ruc: ruc,
                    RazonSocial: null,
                    DireccionFiscal: null,
                    Departamento: null,
                    Provincia: null,
                    Distrito: null,
                    Ubigeo: null,
                    Estado: null,
                    Condicion: null,
                    RepresentanteLegal: null,
                    TipoEmision: null,
                    Fuente: null,
                    EsHabido: false,
                    EsActivo: false
                );
            }

            try
            {
                var client = _httpClientFactory.CreateClient("SunatClient");
                var response = await client.GetAsync($"https://api.apis.net.pe/v1/ruc?numero={ruc}", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    var razonSocial = root.TryGetProperty("nombre", out var n) ? n.GetString() ?? "" : "";
                    var direccion = root.TryGetProperty("direccion", out var d) ? d.GetString() ?? "" : "";
                    var estado = root.TryGetProperty("estado", out var e) ? e.GetString()?.Trim().ToUpper() : null;
                    var condicion = root.TryGetProperty("condicion", out var c) ? c.GetString()?.Trim().ToUpper() : null;
                    estado = string.IsNullOrWhiteSpace(estado) ? "NO VERIFICADO" : estado;
                    condicion = string.IsNullOrWhiteSpace(condicion) ? "NO VERIFICADO" : condicion;
                    var representanteLegal = root.TryGetProperty("representanteLegal", out var rep) ? rep.GetString()?.Trim() : null;
                    var departamento = root.TryGetProperty("departamento", out var dep) ? dep.GetString() ?? "LIMA" : "LIMA";
                    var provincia = root.TryGetProperty("provincia", out var prov) ? prov.GetString() ?? "LIMA" : "LIMA";
                    var distrito = root.TryGetProperty("distrito", out var dist) ? dist.GetString() ?? "" : "";
                    var ubigeo = root.TryGetProperty("ubigeo", out var u) ? u.GetString() ?? "150101" : "150101";

                    bool esHabido = condicion == "HABIDO";
                    bool esActivo = estado == "ACTIVO";

                    return new SunatConsultaResult(
                        Success: true,
                        Message: null,
                        Ruc: ruc,
                        RazonSocial: razonSocial,
                        DireccionFiscal: direccion,
                        Departamento: departamento,
                        Provincia: provincia,
                        Distrito: distrito,
                        Ubigeo: ubigeo,
                        Estado: estado,
                        Condicion: condicion,
                        RepresentanteLegal: representanteLegal,
                        TipoEmision: ruc.StartsWith("20") ? "FACTURA ELECTRÓNICA (TIPO 01)" : "BOLETA / FACTURA ELECTRÓNICA",
                        Fuente: "Padrón SUNAT Oficial (En Línea)",
                        EsHabido: esHabido,
                        EsActivo: esActivo
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al consultar API externa de SUNAT para RUC {Ruc}", ruc);
            }

            return new SunatConsultaResult(
                Success: false,
                Message: "No se encontró información en el padrón de SUNAT para este RUC o el servicio externo no respondió. Puede ingresar los datos manualmente.",
                Ruc: ruc,
                RazonSocial: null,
                DireccionFiscal: null,
                Departamento: null,
                Provincia: null,
                Distrito: null,
                Ubigeo: null,
                Estado: null,
                Condicion: null,
                RepresentanteLegal: null,
                TipoEmision: null,
                Fuente: null,
                EsHabido: false,
                EsActivo: false
            );
        }
    }
}
