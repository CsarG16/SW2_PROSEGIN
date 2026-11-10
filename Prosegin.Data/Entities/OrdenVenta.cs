namespace Prosegin.Data.Entities;

public class OrdenVenta
{
    public int Id { get; set; }
    public int CotizacionId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; }
    
    // PorPreparar, EnPreparacion, ListoParaDespacho, EnRuta, Entregado, Cancelado
    public string EstadoLogistico { get; set; } = "PorPreparar";
    public string? MotivoCancelacion { get; set; }

    // Navegación
    public Cotizacion Cotizacion { get; set; } = null!;
    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();
    public ICollection<GuiaRemision> GuiasRemision { get; set; } = new List<GuiaRemision>();
    public ICollection<OrdenCompra> OrdenesCompra { get; set; } = new List<OrdenCompra>();
}
