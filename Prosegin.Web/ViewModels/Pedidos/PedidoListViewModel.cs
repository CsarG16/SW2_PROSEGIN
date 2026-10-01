namespace Prosegin.Web.ViewModels.Pedidos;

public class PedidoListViewModel
{
    public string? Termino { get; set; }
    public string EstadoFiltro { get; set; } = "ALL";

    public List<PedidoDetalleViewModel> Pedidos { get; set; } = new();

    public int TotalPedidos { get; set; }
    public int TotalPorPreparar { get; set; }
    public int TotalEnPreparacion { get; set; }
    public int TotalListoDespacho { get; set; }
    public int TotalUrgentes { get; set; }

    public string MensajeSinResultados { get; set; } = Prosegin.Data.Validation.PedidoLogisticaRules.MensajeSinResultados;
}
