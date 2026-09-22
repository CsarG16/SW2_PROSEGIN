namespace Prosegin.Data.Entities;

public class CotizacionDetalle
{
    public int Id { get; set; }
    public int CotizacionId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    
    public decimal CostoProveedorReferencial { get; set; }
    public decimal MargenDeseado { get; set; } // Ejemplo: 0.30 para 30%
    public decimal PrecioVentaCalculado { get; set; }
    public decimal Subtotal { get; set; }

    // Navegación
    public Cotizacion Cotizacion { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
