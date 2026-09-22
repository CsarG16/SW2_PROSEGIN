namespace Prosegin.Data.Entities;

public class OrdenCompra
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public int? OrdenVentaId { get; set; } // Nullable, puede no estar ligada a una OV directa en un futuro
    public DateTime FechaCompra { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Recibida
    public decimal Total { get; set; }

    // Navegación
    public Proveedor Proveedor { get; set; } = null!;
    public OrdenVenta? OrdenVenta { get; set; }
    public ICollection<OrdenCompraDetalle> Detalles { get; set; } = new List<OrdenCompraDetalle>();
}
