namespace Prosegin.Web.ViewModels.Cotizaciones;

public class CotizacionBandejaViewModel
{
    public string EstadoFiltro { get; set; } = "Todas";
    public string Busqueda { get; set; } = string.Empty;
    public int Pagina { get; set; } = 1;
    public int? PanelAbiertoId { get; set; }
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
    public string? NumeroOrdenCompraCliente { get; set; }
    public string? NombreArchivoOrdenCompraCliente { get; set; }
    public bool PuedeReemplazarOrdenCompra { get; set; }
    public List<CotizacionBandejaDetalleViewModel> Detalles { get; set; } = new();
}

public class CotizacionBandejaDetalleViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}
