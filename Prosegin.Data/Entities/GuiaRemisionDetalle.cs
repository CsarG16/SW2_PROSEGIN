namespace Prosegin.Data.Entities;

public class GuiaRemisionDetalle
{
    public int Id { get; set; }
    public int GuiaRemisionId { get; set; }
    public int ProductoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = "NIU";
    public decimal Cantidad { get; set; }
    public decimal PesoUnitarioKg { get; set; }
    public decimal PesoTotalKg { get; set; }

    public GuiaRemision GuiaRemision { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
