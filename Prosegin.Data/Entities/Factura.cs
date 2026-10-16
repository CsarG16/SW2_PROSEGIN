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
    public string EstadoEmision { get; set; } = "Pendiente_Envio";
    public string Serie { get; set; } = string.Empty;
    public int Numero { get; set; }
    public string? CodigoHash { get; set; }
    public string? CadenaQr { get; set; }
    public string? EnlacePdf { get; set; }
    public string? EnlaceXml { get; set; }
    public string? EnlaceCdr { get; set; }
    public string? MensajeError { get; set; }

    // Navegación
    public OrdenVenta OrdenVenta { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
    public ICollection<FacturaDetalle> Detalles { get; set; } = new List<FacturaDetalle>();
}
