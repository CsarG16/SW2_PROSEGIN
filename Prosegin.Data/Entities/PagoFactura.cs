namespace Prosegin.Data.Entities;

public class PagoFactura
{
    public int Id { get; set; }
    public int FacturaId { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaPago { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Factura Factura { get; set; } = null!;
}
