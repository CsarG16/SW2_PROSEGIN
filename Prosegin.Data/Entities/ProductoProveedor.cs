namespace Prosegin.Data.Entities;

public class ProductoProveedor
{
    public int ProductoId { get; set; }
    public int ProveedorId { get; set; }
    public decimal CostoCompra { get; set; }
    public bool EsPrincipal { get; set; }
    public int PlazoEntregaHoras { get; set; } = 24;

    public Producto Producto { get; set; } = null!;
    public Proveedor Proveedor { get; set; } = null!;
}
