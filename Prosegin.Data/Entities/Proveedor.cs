namespace Prosegin.Data.Entities;

public class Proveedor
{
    public int Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string Especialidad { get; set; } = string.Empty;
    public string CondicionPago { get; set; } = string.Empty;
    public string UbicacionMalvinas { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string CorreoComercial { get; set; } = string.Empty;
    public string Contacto { get; set; } = string.Empty;
    public string EstadoSunat { get; set; } = "NO VERIFICADO";
    public string CondicionSunat { get; set; } = "NO VERIFICADO";
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Navegación
    public ICollection<ProductoProveedor> ProductosAutorizados { get; set; } = new List<ProductoProveedor>();
    public ICollection<OrdenCompra> OrdenesCompra { get; set; } = new List<OrdenCompra>();
    public ICollection<ContactoProveedor> Contactos { get; set; } = new List<ContactoProveedor>();
    public ICollection<CuentaBancariaProveedor> CuentasBancarias { get; set; } = new List<CuentaBancariaProveedor>();
}
