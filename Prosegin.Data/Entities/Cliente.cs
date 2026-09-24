namespace Prosegin.Data.Entities;

public class Cliente
{
    public int Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string CorreoElectronico { get; set; } = string.Empty;
    public string? Departamento { get; set; }
    public string? Provincia { get; set; }
    public string? Distrito { get; set; }
    public string? Ubigeo { get; set; }
    public string EstadoSunat { get; set; } = "ACTIVO";
    public string CondicionSunat { get; set; } = "HABIDO";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public bool Activo { get; set; } = true;

    // Navegación
    public ICollection<PuntoEntrega> PuntosEntrega { get; set; } = new List<PuntoEntrega>();
    public ICollection<ContactoCliente> Contactos { get; set; } = new List<ContactoCliente>();
    public ICollection<Cotizacion> Cotizaciones { get; set; } = new List<Cotizacion>();
    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();
}
