namespace Prosegin.Data.Entities;

public class ContactoProveedor
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; }

    public Proveedor Proveedor { get; set; } = null!;
}
