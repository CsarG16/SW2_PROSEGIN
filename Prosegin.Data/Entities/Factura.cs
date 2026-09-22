namespace Prosegin.Data.Entities;

public class Factura
{
    public int Id { get; set; }
    public int OrdenVentaId { get; set; }
    public int ClienteId { get; set; }
    
    public string NumeroFactura { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public DateTime FechaVencimiento { get; set; }
    
    // Pendiente, Pagado, Vencido
    public string EstadoPago { get; set; } = "Pendiente";
    
    public decimal Total { get; set; }

    // Navegación
    public OrdenVenta OrdenVenta { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
}
