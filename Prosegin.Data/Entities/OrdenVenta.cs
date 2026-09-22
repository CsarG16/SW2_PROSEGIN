namespace Prosegin.Data.Entities;

public class OrdenVenta
{
    public int Id { get; set; }
    public int CotizacionId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    
    // EnPreparacion, EnConsolidacion, Despachado, Entregado
    public string EstadoLogistico { get; set; } = "EnPreparacion";

    // Navegación
    public Cotizacion Cotizacion { get; set; } = null!;
    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();
    public ICollection<OrdenCompra> OrdenesCompra { get; set; } = new List<OrdenCompra>();
}
