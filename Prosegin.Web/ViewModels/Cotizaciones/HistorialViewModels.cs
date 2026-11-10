namespace Prosegin.Web.ViewModels.Cotizaciones;

public class HistorialViewModel
{
    public string Busqueda { get; set; } = string.Empty;
    public string? Mensaje { get; set; }
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int TotalRegistros { get; set; }
    public List<HistorialDocumentoViewModel> Registros { get; set; } = new();
}

public class HistorialDocumentoViewModel
{
    public string Tipo { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public int Id { get; set; }
    public string NumeroPedido { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string NumeroDocumentoCliente { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool PdfDisponible { get; set; }
}

public class HistorialDetalleViewModel
{
    public HistorialDocumentoViewModel Documento { get; set; } = new();
    public string? NumeroFactura { get; set; }
    public string? EstadoFactura { get; set; }
    public List<HistorialDetalleItemViewModel> Items { get; set; } = new();
}

public class HistorialDetalleItemViewModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}
