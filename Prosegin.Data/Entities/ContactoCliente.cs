namespace Prosegin.Data.Entities;

public class ContactoCliente
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; } = false;

    // Navegación
    public Cliente Cliente { get; set; } = null!;
}
