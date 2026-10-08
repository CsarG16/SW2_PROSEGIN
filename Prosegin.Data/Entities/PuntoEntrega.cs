namespace Prosegin.Data.Entities;

public class PuntoEntrega
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string? TipoSede { get; set; }
    public string? NombreAlias { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public string? Departamento { get; set; }
    public string? Provincia { get; set; }
    public string? Distrito { get; set; }
    public string? Ubigeo { get; set; }
    public string? ContactoRecepcion { get; set; }
    public string? TelefonoMovil { get; set; }
    public string? HorarioRecepcion { get; set; }
    public string? RestriccionesAcceso { get; set; }
    public bool EsPredeterminada { get; set; } = false;
    public bool Activo { get; set; } = true;

    // Navegación
    public Cliente Cliente { get; set; } = null!;
}
