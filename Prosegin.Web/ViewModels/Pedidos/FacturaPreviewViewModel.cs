namespace Prosegin.Web.ViewModels.Pedidos;

public class FacturaPreviewViewModel
{
    public int OrdenVentaId { get; set; }
    public string NumeroPedido { get; set; } = string.Empty;
    public string NumeroOrdenCompra { get; set; } = string.Empty;
    public string ClienteRazonSocial { get; set; } = string.Empty;
    public string ClienteRuc { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string CondicionPago { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public string Serie { get; set; } = string.Empty;
    public int Numero { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public List<PedidoItemViewModel> Items { get; set; } = new();
}
