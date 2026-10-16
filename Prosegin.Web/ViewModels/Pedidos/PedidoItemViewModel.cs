namespace Prosegin.Web.ViewModels.Pedidos;

public class PedidoItemViewModel
{
    public int ProductoId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string UnidadMedida { get; set; } = "Und";
    public string Codigo { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
}
