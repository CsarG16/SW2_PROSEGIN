namespace Prosegin.Web.ViewModels.Pedidos;

public class PedidoItemViewModel
{
    public int ProductoId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string UnidadMedida { get; set; } = "Und";
}
