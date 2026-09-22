namespace Prosegin.Data.Entities;

public class PuntoEntrega
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public bool Activo { get; set; } = true;

    // Navegación
    public Cliente Cliente { get; set; } = null!;
}
