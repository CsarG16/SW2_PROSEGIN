using Prosegin.Web.ViewModels.Pedidos;

namespace Prosegin.Web.Services;

public interface IPedidosService
{
    Task<PedidoListViewModel> ObtenerPedidosConfirmadosAsync(string? termino, string? estado, int pagina = 1, CancellationToken cancellationToken = default);
    Task<PedidoDetalleViewModel?> ObtenerDetallePedidoAsync(string numeroPedido, CancellationToken cancellationToken = default);
}
