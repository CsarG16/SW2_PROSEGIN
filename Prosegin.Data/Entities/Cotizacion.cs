namespace Prosegin.Data.Entities;

public class Cotizacion
{
    public int Id { get; set; }
    public string Correlativo { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public DateTime? FechaVencimiento { get; set; }
    public string Estado { get; set; } = "Borrador"; // Borrador, Enviada, Aprobada, Rechazada
    public string CondicionPago { get; set; } = "Contado"; // Contado, 7 dias, 15 dias, 30 dias
    
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }

    // Navegación
    public Cliente Cliente { get; set; } = null!;
    public ICollection<CotizacionDetalle> Detalles { get; set; } = new List<CotizacionDetalle>();
    public OrdenVenta? OrdenVenta { get; set; }
}
