using Microsoft.AspNetCore.Mvc;
using Prosegin.Data;
using Prosegin.Web.Services;

namespace Prosegin.Web.Controllers;

public class PedidosController : Controller
{
    private readonly IPedidosService _pedidosService;

    public PedidosController(ProseginDbContext context)
    {
        _pedidosService = new PedidosService(context);
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? termino,
        string? estado,
        CancellationToken cancellationToken)
    {
        var model = await _pedidosService.ObtenerPedidosConfirmadosAsync(termino, estado, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("El número de pedido es obligatorio.");
        }

        var detalle = await _pedidosService.ObtenerDetallePedidoAsync(id, cancellationToken);
        if (detalle == null)
        {
            return NotFound("Pedido no encontrado.");
        }

        return Json(detalle);
    }
}
