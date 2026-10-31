namespace Prosegin.Web.ViewModels.Pedidos;

public class RecepcionMercaderiaViewModel
{
    public string NumeroPedido { get; set; } = string.Empty;
    public string NumeroOrdenCompra { get; set; } = string.Empty;
    public string DocumentoReferencia { get; set; } = string.Empty;
    public int Revision { get; set; }
    public DateTime FechaDocumento { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string Almacen { get; set; } = "Central Huachipa";
    public List<RecepcionItemViewModel> Items { get; set; } = new();
}

public class RecepcionItemViewModel
{
    public int ProductoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Proveedor { get; set; } = string.Empty;
    public int CantidadSolicitada { get; set; }
    public int CantidadRecibida { get; set; }
    public string UnidadMedida { get; set; } = "Und";
    public string EstadoInspeccion { get; set; } = "Pendiente";
    public string Observaciones { get; set; } = string.Empty;
}

public class GuardarRecepcionRequest
{
    public string NumeroPedido { get; set; } = string.Empty;
    public bool ConfirmarTotal { get; set; }
    public List<RecepcionItemViewModel> Items { get; set; } = new();
}
