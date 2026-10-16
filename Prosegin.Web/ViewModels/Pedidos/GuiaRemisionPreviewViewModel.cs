namespace Prosegin.Web.ViewModels.Pedidos;

public class GuiaRemisionPreviewViewModel
{
    public int OrdenVentaId { get; set; }
    public string NumeroPedido { get; set; } = string.Empty;
    public string NumeroOrdenCompra { get; set; } = string.Empty;
    public string ClienteRazonSocial { get; set; } = string.Empty;
    public string ClienteRuc { get; set; } = string.Empty;
    public string PuntoPartidaDireccion { get; set; } = string.Empty;
    public string? PuntoPartidaUbigeo { get; set; }
    public string PuntoLlegadaDireccion { get; set; } = string.Empty;
    public string? PuntoLlegadaUbigeo { get; set; }
    public DateTime FechaEmision { get; set; }
    public DateTime FechaInicioTraslado { get; set; }
    public string Serie { get; set; } = string.Empty;
    public int Numero { get; set; }
    public decimal PesoBrutoTotal { get; set; }
    public int NumeroBultos { get; set; }
    public List<GuiaRemisionItemViewModel> Items { get; set; } = new();
}

public class GuiaRemisionItemViewModel
{
    public int ProductoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = "NIU";
    public decimal Cantidad { get; set; }
    public decimal PesoUnitarioKg { get; set; }
    public decimal PesoTotalKg { get; set; }
}

public class EmitirGuiaRemisionRequest
{
    public string NumeroPedido { get; set; } = string.Empty;
    public string ConductorNombre { get; set; } = string.Empty;
    public string? ConductorDocumento { get; set; }
    public string? ConductorLicencia { get; set; }
    public string PlacaVehiculo { get; set; } = string.Empty;
}
