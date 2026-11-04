using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Prosegin.Web.Services;
using Prosegin.Web.ViewModels.Pedidos;

namespace Prosegin.Web.Controllers;

public class PedidosController : Controller
{
    private readonly IPedidosService _pedidosService;
    private readonly IHttpClientFactory _httpClientFactory;

    public PedidosController(IPedidosService pedidosService, IHttpClientFactory httpClientFactory)
    {
        _pedidosService = pedidosService;
        _httpClientFactory = httpClientFactory;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DescargarComprobantes(
        string numeroPedido,
        bool factura,
        bool guia,
        CancellationToken cancellationToken)
    {
        if (!factura && !guia)
            return BadBadRequestComprobante();

        var pedido = await _pedidosService.ObtenerDetallePedidoAsync(numeroPedido, cancellationToken);
        try
        {
            var documentos = await ObtenerDocumentosAsync(pedido, factura, guia, cancellationToken);
            if (documentos.Count == 0)
                return StatusCode(502, new { mensaje = "No se pudo obtener los documentos desde Nubefact. Intente nuevamente." });

            using var archivo = new MemoryStream();
            using (var zip = new ZipArchive(archivo, ZipArchiveMode.Create, true))
            {
                foreach (var documento in documentos)
                {
                    var entrada = zip.CreateEntry(documento.Nombre, CompressionLevel.Optimal);
                    using var salida = entrada.Open();
                    await salida.WriteAsync(documento.Contenido, cancellationToken);
                }
            }

            return File(archivo.ToArray(), "application/zip", $"Comprobantes_{numeroPedido}.zip");
        }
        catch (PdfComprobanteException exception)
        {
            return StatusCode(502, new { mensaje = exception.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarComprobantes(
        string numeroPedido,
        string destinatario,
        bool factura,
        bool guia,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinatario))
            return BadRequest(new { mensaje = "Seleccione un destinatario." });
        if (!factura && !guia)
            return BadRequest(new { mensaje = "Seleccione al menos un comprobante disponible." });

        try
        {
            _ = new MailAddress(destinatario.Trim());
        }
        catch (FormatException)
        {
            return BadRequest(new { mensaje = "El correo del destinatario no es válido." });
        }

        var pedido = await _pedidosService.ObtenerDetallePedidoAsync(numeroPedido, cancellationToken);
        List<(string Nombre, byte[] Contenido)> documentos;
        try
        {
            documentos = await ObtenerDocumentosAsync(pedido, factura, guia, cancellationToken);
        }
        catch (PdfComprobanteException exception)
        {
            return StatusCode(502, new { mensaje = exception.Message });
        }

        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
        if (string.IsNullOrWhiteSpace(smtpHost))
            return StatusCode(503, new { mensaje = "La configuración SMTP no está disponible." });

        try
        {
            var puerto = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var puertoConfigurado)
                ? puertoConfigurado : 587;
            using var smtp = new SmtpClient(smtpHost, puerto)
            {
                EnableSsl = !string.Equals(Environment.GetEnvironmentVariable("SMTP_SSL"), "false", StringComparison.OrdinalIgnoreCase)
            };
            var usuario = Environment.GetEnvironmentVariable("SMTP_USER");
            var password = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
            if (!string.IsNullOrWhiteSpace(usuario))
                smtp.Credentials = new NetworkCredential(usuario, password);

            var remitente = Environment.GetEnvironmentVariable("SMTP_FROM") ?? "no-reply@prosegin.com";
            using var mensaje = new MailMessage(remitente, destinatario.Trim())
            {
                Subject = $"Comprobantes tributarios - Pedido {numeroPedido}",
                Body = $"Estimado cliente:\n\nAdjuntamos los comprobantes tributarios correspondientes al pedido {numeroPedido}.",
                IsBodyHtml = false
            };
            foreach (var documento in documentos)
                mensaje.Attachments.Add(new Attachment(new MemoryStream(documento.Contenido), documento.Nombre, "application/pdf"));

            await smtp.SendMailAsync(mensaje, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or HttpRequestException)
        {
            return StatusCode(502, new { mensaje = "No se pudieron enviar los comprobantes. Verifique la configuración SMTP." });
        }

        return Ok(new { mensaje = "Comprobantes enviados exitosamente al destinatario" });
    }

    private async Task<List<(string Nombre, byte[] Contenido)>> ObtenerDocumentosAsync(
        PedidoDetalleViewModel? pedido,
        bool factura,
        bool guia,
        CancellationToken cancellationToken)
    {
        var documentos = new List<(string Nombre, byte[] Contenido)>();
        if (pedido == null)
            return documentos;

        if (factura && !string.IsNullOrWhiteSpace(pedido.EnlacePdfFactura))
        {
            var contenido = await ObtenerPdfAsync(pedido.EnlacePdfFactura, "factura", cancellationToken);
            documentos.Add(($"{pedido.NumeroFactura ?? "Factura"}.pdf", contenido));
        }

        if (guia && !string.IsNullOrWhiteSpace(pedido.EnlacePdfGuia))
        {
            var contenido = await ObtenerPdfAsync(pedido.EnlacePdfGuia, "guía", cancellationToken);
            documentos.Add(($"{pedido.NumeroGuiaRemision ?? "GuiaRemision"}.pdf", contenido));
        }

        return documentos;
    }

    private async Task<byte[]> ObtenerPdfAsync(string enlace, string tipoDocumento, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(enlace, UriKind.Absolute, out var uri))
            throw new PdfComprobanteException($"No se pudo obtener el PDF de la {tipoDocumento} desde Nubefact. Intente nuevamente.");

        var httpClient = _httpClientFactory.CreateClient("ComprobantesClient");
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new PdfComprobanteException($"No se pudo obtener el PDF de la {tipoDocumento} desde Nubefact. Intente nuevamente.");

            var contenido = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (contenido.Length == 0)
                throw new PdfComprobanteException($"No se pudo obtener el PDF de la {tipoDocumento} desde Nubefact. Intente nuevamente.");
            return contenido;
        }
        catch (HttpRequestException)
        {
            throw new PdfComprobanteException($"No se pudo obtener el PDF de la {tipoDocumento} desde Nubefact. Intente nuevamente.");
        }
        catch (TaskCanceledException)
        {
            throw new PdfComprobanteException("La descarga del comprobante desde Nubefact tardó demasiado. Intente nuevamente.");
        }
    }

    private BadRequestObjectResult BadBadRequestComprobante() =>
        BadRequest(new { mensaje = "Seleccione al menos un comprobante disponible." });

    private sealed class PdfComprobanteException : Exception
    {
        public PdfComprobanteException(string mensaje) : base(mensaje) { }
    }

    [HttpGet]
    public async Task<IActionResult> Recepcion(string numeroPedido, CancellationToken cancellationToken)
    {
        var model = await _pedidosService.ObtenerRecepcionAsync(numeroPedido, cancellationToken);
        if (model == null)
        {
            var ordenTieneCompras = await _pedidosService.TieneOrdenesCompraAsync(numeroPedido, cancellationToken);
            TempData["ToastError"] = ordenTieneCompras
                ? "El pedido debe estar En Preparación para controlar la recepción"
                : "El pedido no tiene órdenes de compra emitidas";
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
        if (request.ConfirmarTotal)
            TempData["ToastSuccess"] = resultado.Mensaje;
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
