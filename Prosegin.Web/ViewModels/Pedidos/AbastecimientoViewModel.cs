namespace Prosegin.Web.ViewModels.Pedidos;

public class AbastecimientoViewModel
{
    public int OrdenVentaId { get; set; }
    public string NumeroPedido { get; set; } = string.Empty;
    public string NumeroOrdenCompraCliente { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string DireccionEntrega { get; set; } = string.Empty;
    public List<AbastecimientoProductoViewModel> Productos { get; set; } = new();
    public string? Error { get; set; }
}

public class AbastecimientoProductoViewModel
{
    public int ProductoId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public int? ProveedorSeleccionadoId { get; set; }
    public List<AbastecimientoProveedorOpcionViewModel> Proveedores { get; set; } = new();
}

public class AbastecimientoProveedorOpcionViewModel
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public decimal CostoUnitario { get; set; }
    public int PlazoEntregaHoras { get; set; }
    public bool EsPrincipal { get; set; }
}

public class OrdenProveedorSeleccionViewModel
{
    public int ProductoId { get; set; }
    public int ProveedorId { get; set; }
}
