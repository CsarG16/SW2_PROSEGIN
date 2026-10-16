namespace Prosegin.Data.Entities;

public class FacturaDetalle
{
    public int Id { get; set; }
    public int FacturaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = "NIU";
    public decimal Cantidad { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }

    public Factura Factura { get; set; } = null!;
}
