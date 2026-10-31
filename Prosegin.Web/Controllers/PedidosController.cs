using Microsoft.AspNetCore.Mvc;
using Prosegin.Web.Services;
using Prosegin.Web.ViewModels.Pedidos;

namespace Prosegin.Web.Controllers;

public class PedidosController : Controller
{
    private readonly IPedidosService _pedidosService;

    public PedidosController(IPedidosService pedidosService)
    {
        _pedidosService = pedidosService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? termino,
        string? estado,
        CancellationToken cancellationToken,
        int pagina = 1)
    {
        var model = await _pedidosService.ObtenerPedidosConfirmadosAsync(termino, estado, pagina, cancellationToken);
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

    [HttpGet]
    public async Task<IActionResult> Recepcion(string numeroPedido, CancellationToken cancellationToken)
    {
        var model = await _pedidosService.ObtenerRecepcionAsync(numeroPedido, cancellationToken);
        if (model == null)
        {
            TempData["ToastError"] = "El pedido debe estar En Preparación para controlar la recepción";
            return RedirectToAction(nameof(Index), new { termino = numeroPedido });
        }

        ViewData["Title"] = "Control de Recepción";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarRecepcion([FromBody] GuardarRecepcionRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _pedidosService.GuardarRecepcionAsync(request, cancellationToken);
        if (!resultado.Exito)
            return UnprocessableEntity(new { mensaje = resultado.Mensaje });
        return Ok(new { mensaje = resultado.Mensaje });
    }

    [HttpGet]
    public async Task<IActionResult> Factura(string numeroPedido, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
        {
            return RedirectToAction(nameof(Index));
        }

        var preview = await _pedidosService.PrevisualizarFacturaAsync(numeroPedido, cancellationToken);
        if (preview == null)
        {
            TempData["ToastError"] = "El pedido no está entregado, el cliente no está activo y habido en SUNAT o ya tiene una factura emitida.";
            return RedirectToAction(nameof(Index), new { termino = numeroPedido });
        }

        ViewData["Title"] = $"Factura Electrónica: {preview.NumeroPedido}";
        return View(preview);
    }

    [HttpGet]
    public async Task<IActionResult> PrevisualizarFactura(string numeroPedido, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
            return BadRequest("El número de pedido es obligatorio.");

        var preview = await _pedidosService.PrevisualizarFacturaAsync(numeroPedido, cancellationToken);
        if (preview == null)
            return Conflict("El pedido no está entregado, el cliente no está activo y habido en SUNAT o ya tiene una factura.");

        return Json(preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmitirFactura(string numeroPedido, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
            return BadRequest(new { mensaje = "El número de pedido es obligatorio." });

        var resultado = await _pedidosService.EmitirFacturaAsync(numeroPedido, cancellationToken);
        if (resultado.Exito)
        {
            return Json(new
            {
                exito = true,
                mensaje = "Factura emitida con éxito",
                numeroFactura = resultado.NumeroFactura,
                pdf = resultado.Respuesta?.EnlacePdf,
                xml = resultado.Respuesta?.EnlaceXml,
                cdr = resultado.Respuesta?.EnlaceCdr
            });
        }

        return UnprocessableEntity(new
        {
            exito = false,
            pendiente = resultado.QuedoPendiente,
            mensaje = resultado.Mensaje
        });
    }

    [HttpGet]
    public async Task<IActionResult> GuiaRemision(string numeroPedido, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
        {
            return RedirectToAction(nameof(Index));
        }

        var preview = await _pedidosService.PrevisualizarGuiaRemisionAsync(numeroPedido, cancellationToken);
        if (preview == null)
        {
            TempData["ToastError"] = "El pedido no está listo para despacho, ya tiene una guía emitida o hay productos sin peso registrado.";
            return RedirectToAction(nameof(Index), new { termino = numeroPedido });
        }

        ViewData["Title"] = $"Guía de Remisión: {preview.NumeroPedido}";
        return View(preview);
    }

    [HttpGet]
    public async Task<IActionResult> PrevisualizarGuiaRemision(string numeroPedido, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
            return BadRequest("El número de pedido es obligatorio.");
        var preview = await _pedidosService.PrevisualizarGuiaRemisionAsync(numeroPedido, cancellationToken);
        if (preview == null)
            return Conflict("El pedido no está listo para despacho, ya tiene una guía o hay productos sin peso registrado.");
        return Json(preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmitirGuiaRemision(
        [FromForm] EmitirGuiaRemisionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NumeroPedido))
            return BadRequest(new { mensaje = "El número de pedido es obligatorio." });
        var resultado = await _pedidosService.EmitirGuiaRemisionAsync(request, cancellationToken);
        if (resultado.Exito)
        {
            return Json(new
            {
                exito = true,
                mensaje = "Guía de remisión emitida con éxito",
                numeroGuia = resultado.NumeroGuia,
                pdf = resultado.Respuesta?.EnlacePdf,
                xml = resultado.Respuesta?.EnlaceXml,
                cdr = resultado.Respuesta?.EnlaceCdr
            });
        }
        return UnprocessableEntity(new { exito = false, pendiente = resultado.QuedoPendiente, mensaje = resultado.Mensaje });
    }
}
