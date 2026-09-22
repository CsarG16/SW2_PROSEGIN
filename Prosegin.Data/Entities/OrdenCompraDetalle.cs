namespace Prosegin.Data.Entities;

public class OrdenCompraDetalle
{
    public int Id { get; set; }
    public int OrdenCompraId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal { get; set; }

    // Navegación
    public OrdenCompra OrdenCompra { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
