using Prosegin.Web.ViewModels.Pedidos;

namespace Prosegin.Web.Services;

public interface IPedidosService
{
    Task<PedidoListViewModel> ObtenerPedidosConfirmadosAsync(string? termino, string? estado, int pagina = 1, CancellationToken cancellationToken = default);
    Task<(bool Exito, string Mensaje)> ActualizarEstadoLogisticoAsync(string numeroPedido, string nuevoEstado, CancellationToken cancellationToken = default);
    Task<(bool Exito, string Mensaje)> CancelarPedidoAsync(string numeroPedido, string? motivo, CancellationToken cancellationToken = default);
    Task<PedidoDetalleViewModel?> ObtenerDetallePedidoAsync(string numeroPedido, CancellationToken cancellationToken = default);
    Task<FacturaPreviewViewModel?> PrevisualizarFacturaAsync(string numeroPedido, CancellationToken cancellationToken = default);
    Task<EmitirFacturaResult> EmitirFacturaAsync(string numeroPedido, CancellationToken cancellationToken = default);
    Task<GuiaRemisionPreviewViewModel?> PrevisualizarGuiaRemisionAsync(string numeroPedido, CancellationToken cancellationToken = default);
    Task<EmitirGuiaRemisionResult> EmitirGuiaRemisionAsync(EmitirGuiaRemisionRequest request, CancellationToken cancellationToken = default);
    Task<RecepcionMercaderiaViewModel?> ObtenerRecepcionAsync(string numeroPedido, CancellationToken cancellationToken = default);
    Task<bool> TieneOrdenesCompraAsync(string numeroPedido, CancellationToken cancellationToken = default);
    Task<(bool Exito, string Mensaje)> GuardarRecepcionAsync(GuardarRecepcionRequest request, CancellationToken cancellationToken = default);
}
