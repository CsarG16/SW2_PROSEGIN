namespace Prosegin.Web.ViewModels.Cotizaciones;

public class CotizacionBandejaViewModel
{
    public string EstadoFiltro { get; set; } = "Todas";
    public string Busqueda { get; set; } = string.Empty;
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int TotalResultados { get; set; }
    public int ResultadosTodas { get; set; }
    public int ResultadosEnviadas { get; set; }
    public int ResultadosVencidas { get; set; }
    public int ResultadosAprobadas { get; set; }
    public List<CotizacionBandejaItemViewModel> Cotizaciones { get; set; } = new();
}

public class CotizacionBandejaItemViewModel
{
    public int Id { get; set; }
    public string Correlativo { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
}
