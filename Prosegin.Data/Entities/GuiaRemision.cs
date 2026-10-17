namespace Prosegin.Data.Entities;

public class GuiaRemision
{
    public int Id { get; set; }
    public int OrdenVentaId { get; set; }
    public int ClienteId { get; set; }
    public string NumeroGuia { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public int Numero { get; set; }
    public string EstadoEmision { get; set; } = "Pendiente_Envio";
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public DateTime FechaInicioTraslado { get; set; }
    public decimal PesoBrutoTotal { get; set; }
    public int NumeroBultos { get; set; }
    public string PuntoPartidaDireccion { get; set; } = string.Empty;
    public string? PuntoPartidaUbigeo { get; set; }
    public string PuntoLlegadaDireccion { get; set; } = string.Empty;
    public string? PuntoLlegadaUbigeo { get; set; }
    public string ConductorNombre { get; set; } = string.Empty;
    public string? ConductorDocumento { get; set; }
    public string? ConductorLicencia { get; set; }
    public string PlacaVehiculo { get; set; } = string.Empty;
    public string? CodigoHash { get; set; }
    public string? CadenaQr { get; set; }
    public string? EnlacePdf { get; set; }
    public string? EnlaceXml { get; set; }
    public string? EnlaceCdr { get; set; }
    public string? MensajeError { get; set; }

    public OrdenVenta OrdenVenta { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
    public ICollection<GuiaRemisionDetalle> Detalles { get; set; } = new List<GuiaRemisionDetalle>();
}
