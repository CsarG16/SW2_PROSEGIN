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
    public int Pagina { get; set; } = 1;
    public int TamanoPagina { get; set; } = 5;
    public int TotalFiltrados { get; set; }
    public int TotalPaginas { get; set; } = 1;
    public int Desde => TotalFiltrados == 0 ? 0 : (Pagina - 1) * TamanoPagina + 1;
    public int Hasta => TotalFiltrados == 0 ? 0 : Desde + Pedidos.Count - 1;

    public string MensajeSinResultados { get; set; } = Prosegin.Data.Validation.PedidoLogisticaRules.MensajeSinResultados;
}
