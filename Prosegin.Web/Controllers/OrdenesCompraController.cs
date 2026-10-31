using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Web.Services;
using Prosegin.Web.ViewModels.Pedidos;

namespace Prosegin.Web.Controllers;

public class OrdenesCompraController : Controller
{
    private readonly ProseginDbContext _context;

    public OrdenesCompraController(ProseginDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? termino, CancellationToken cancellationToken)
    {
        var model = new DirectorioOrdenesCompraViewModel
        {
            Termino = termino?.Trim() ?? string.Empty
        };

        if (termino != null && string.IsNullOrWhiteSpace(termino))
        {
            model.Mensaje = "Ingrese un número de orden, proveedor o pedido para buscar";
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(termino)
            && !Regex.IsMatch(termino.Trim(), @"^[\p{L}\p{N}\s\-\.]+$"))
        {
            model.Mensaje = "Ingrese un número de orden, proveedor o pedido válido";
            return View(model);
        }

        var filtro = termino?.Trim();
        var pedidos = await _context.OrdenesVenta
            .AsNoTracking()
            .Include(orden => orden.Cotizacion)
                .ThenInclude(cotizacion => cotizacion.Cliente)
            .Include(orden => orden.OrdenesCompra)
                .ThenInclude(compra => compra.Proveedor)
            .Include(orden => orden.OrdenesCompra)
                .ThenInclude(compra => compra.Detalles)
            .OrderByDescending(orden => orden.FechaCreacion)
            .ToListAsync(cancellationToken);

        model.Pedidos = pedidos
            .Select(orden => new PedidoOrdenesCompraViewModel
            {
                Id = orden.Id,
                NumeroPedido = orden.Cotizacion.Correlativo,
                Cliente = orden.Cotizacion.Cliente.RazonSocial,
                ClienteRuc = orden.Cotizacion.Cliente.Ruc,
                OrdenesCompra = orden.OrdenesCompra
                    .OrderBy(compra => compra.Correlativo)
                    .Select(compra => new OrdenCompraDirectorioViewModel
                    {
                        Id = compra.Id,
                        NumeroOrdenCompra = compra.Correlativo,
                        Proveedor = compra.Proveedor.RazonSocial,
                            ProveedorRuc = compra.Proveedor.Ruc,
                            DocumentoDisponible = compra.Detalles.Count > 0
                    })
                    .ToList()
            })
            .Where(pedido => string.IsNullOrWhiteSpace(filtro)
                || pedido.NumeroPedido.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                || pedido.OrdenesCompra.Any(orden =>
                    orden.NumeroOrdenCompra.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                    || orden.Proveedor.Contains(filtro, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(filtro) && model.Pedidos.Count == 0)
        {
            model.Mensaje = "No se encontraron órdenes de compra para la búsqueda";
        }

        ViewData["Title"] = "Directorio de Órdenes de Compra Emitidas";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> VerPdf(int id, CancellationToken cancellationToken)
    {
        var orden = await ObtenerOrdenPdfAsync(id, cancellationToken);
        if (!DocumentoDisponible(orden))
        {
            TempData["ToastError"] = "El documento PDF de la orden de compra no está disponible";
            return RedirectToAction(nameof(Index));
        }

        var nombrePdf = NombreArchivoPdf(orden!);
        Response.Headers.ContentDisposition =
            new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline")
            {
                FileNameStar = nombrePdf
            }.ToString();
        return File(OrdenCompraPdfGenerator.Generate(orden!), "application/pdf");
    }

    [HttpGet]
    public async Task<IActionResult> Descargar(int id, CancellationToken cancellationToken)
    {
        var orden = await ObtenerOrdenPdfAsync(id, cancellationToken);
        if (!DocumentoDisponible(orden))
        {
            TempData["ToastError"] = "No se pudo descargar el documento, el archivo no está disponible";
            return RedirectToAction(nameof(Index));
        }

        return File(
            OrdenCompraPdfGenerator.Generate(orden!),
            "application/pdf",
            NombreArchivoPdf(orden!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reenviar(int id, CancellationToken cancellationToken)
    {
        var orden = await ObtenerOrdenPdfAsync(id, cancellationToken);
        if (!DocumentoDisponible(orden))
        {
            TempData["ToastError"] = "El documento PDF de la orden de compra no está disponible";
            return RedirectToAction(nameof(Index));
        }

        var proveedorOrden = await _context.Proveedores
            .AsNoTracking()
            .Include(proveedor => proveedor.Contactos)
            .FirstOrDefaultAsync(proveedor => proveedor.Id == orden!.ProveedorId, cancellationToken);

        var correo = proveedorOrden?.CorreoComercial?.Trim();
        if (string.IsNullOrWhiteSpace(correo))
        {
            correo = proveedorOrden?.Contactos
                .OrderByDescending(contacto => contacto.EsPrincipal)
                .Select(contacto => contacto.Correo)
                .FirstOrDefault(correoContacto => !string.IsNullOrWhiteSpace(correoContacto))
                ?.Trim();
        }
        if (string.IsNullOrWhiteSpace(correo))
        {
            correo = await _context.ContactosProveedor
                .AsNoTracking()
                .Where(contacto => contacto.ProveedorId == orden.ProveedorId
                    && !string.IsNullOrWhiteSpace(contacto.Correo))
                .OrderByDescending(contacto => contacto.EsPrincipal)
                .Select(contacto => contacto.Correo.Trim())
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(correo))
        {
            TempData["ToastError"] = "El proveedor no tiene un correo de contacto registrado";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _ = new MailAddress(correo);
        }
        catch (FormatException)
        {
            TempData["ToastError"] = "El correo de contacto registrado no es válido";
            return RedirectToAction(nameof(Index));
        }

        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            TempData["ToastError"] = "No se pudo enviar el correo: la configuración SMTP no está disponible";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var remitente = Environment.GetEnvironmentVariable("SMTP_FROM") ?? "no-reply@prosegin.com";
            var puerto = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var configuredPort)
                ? configuredPort
                : 587;
            using var smtp = new SmtpClient(smtpHost, puerto)
            {
                EnableSsl = !string.Equals(Environment.GetEnvironmentVariable("SMTP_SSL"), "false", StringComparison.OrdinalIgnoreCase)
            };
            var usuario = Environment.GetEnvironmentVariable("SMTP_USER");
            var contraseña = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
            if (!string.IsNullOrWhiteSpace(usuario))
            {
                smtp.Credentials = new System.Net.NetworkCredential(usuario, contraseña);
            }

            using var mensaje = new MailMessage(remitente, correo)
            {
                Subject = $"Orden de compra {orden!.Correlativo}",
                Body = $"Adjuntamos la orden de compra {orden.Correlativo} emitida por PROSEGIN.",
                IsBodyHtml = false
            };
            mensaje.Attachments.Add(new Attachment(
                new MemoryStream(OrdenCompraPdfGenerator.Generate(orden)),
                NombreArchivoPdf(orden),
                "application/pdf"));
            await smtp.SendMailAsync(mensaje, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            TempData["ToastError"] = "No se pudo enviar el correo. Verifique la configuración SMTP";
            return RedirectToAction(nameof(Index));
        }

        TempData["ToastSuccess"] = "Orden de compra enviada correctamente";
        return RedirectToAction(nameof(Index));
    }

    private Task<Data.Entities.OrdenCompra?> ObtenerOrdenPdfAsync(int id, CancellationToken cancellationToken)
    {
        return _context.OrdenesCompra
            .AsNoTracking()
            .Include(orden => orden.Proveedor)
                .ThenInclude(proveedor => proveedor.Contactos)
            .Include(orden => orden.Detalles)
                .ThenInclude(detalle => detalle.Producto)
            .Include(orden => orden.OrdenVenta)
                .ThenInclude(pedido => pedido!.Cotizacion)
            .FirstOrDefaultAsync(orden => orden.Id == id, cancellationToken);
    }

    private static bool DocumentoDisponible(Data.Entities.OrdenCompra? orden) =>
        orden != null && orden.Detalles.Count > 0;

    private static string NombreArchivoPdf(Data.Entities.OrdenCompra orden)
    {
        var proveedorSinAcentos = orden.Proveedor.RazonSocial.Normalize(System.Text.NormalizationForm.FormD);
        var proveedor = new string(proveedorSinAcentos
            .Where(caracter => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(caracter)
                != System.Globalization.UnicodeCategory.NonSpacingMark
                && (char.IsLetterOrDigit(caracter) || char.IsWhiteSpace(caracter)))
            .ToArray())
            .Trim()
            .Normalize(System.Text.NormalizationForm.FormC);
        var nombreCorto = string.Join("_", proveedor.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return $"Orden_Compra_{orden.Correlativo}_{nombreCorto}.pdf";
    }
}
