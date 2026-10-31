namespace Prosegin.Web.ViewModels.Pedidos;

public class DirectorioOrdenesCompraViewModel
{
    public string Termino { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public List<PedidoOrdenesCompraViewModel> Pedidos { get; set; } = new();
}

public class PedidoOrdenesCompraViewModel
{
    public int Id { get; set; }
    public string NumeroPedido { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string ClienteRuc { get; set; } = string.Empty;
    public List<OrdenCompraDirectorioViewModel> OrdenesCompra { get; set; } = new();
    public int CantidadOrdenes => OrdenesCompra.Count;
}

public class OrdenCompraDirectorioViewModel
{
    public int Id { get; set; }
    public string NumeroOrdenCompra { get; set; } = string.Empty;
    public string Proveedor { get; set; } = string.Empty;
    public string ProveedorRuc { get; set; } = string.Empty;
    public bool DocumentoDisponible { get; set; }
}
