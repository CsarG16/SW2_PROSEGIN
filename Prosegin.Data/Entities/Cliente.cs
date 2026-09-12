namespace Prosegin.Data.Entities;

public class Cliente
{
    public int Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string CorreoElectronico { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public bool Activo { get; set; } = true;
}
