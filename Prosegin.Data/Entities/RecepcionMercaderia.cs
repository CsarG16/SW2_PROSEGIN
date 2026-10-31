namespace Prosegin.Data.Entities;

public class RecepcionMercaderia
{
    public int Id { get; set; }
    public int OrdenVentaId { get; set; }
    public int ProductoId { get; set; }
    public int CantidadSolicitada { get; set; }
    public int CantidadRecibida { get; set; }
    public string EstadoInspeccion { get; set; } = "Pendiente";
    public string Observaciones { get; set; } = string.Empty;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public OrdenVenta OrdenVenta { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
