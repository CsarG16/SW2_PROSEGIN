namespace Prosegin.Data.Entities;

public class Proveedor
{
    public int Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string UbicacionMalvinas { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Contacto { get; set; } = string.Empty;

    // Navegación
    public ICollection<OrdenCompra> OrdenesCompra { get; set; } = new List<OrdenCompra>();
}
