using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Data.Validation;
using Prosegin.Web.Services;
using Prosegin.Web.ViewModels.Cotizaciones;

namespace Prosegin.Web.Controllers;

public class CotizacionesController : Controller
{
    private const int CotizacionesPorPagina = 10;
    private readonly ProseginDbContext _context;
    private readonly ISunatService _sunatService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CotizacionesController> _logger;

    public CotizacionesController(
        ProseginDbContext context,
        ISunatService sunatService,
        IWebHostEnvironment env,
        ILogger<CotizacionesController> logger)
    {
        _context = context;
        _sunatService = sunatService;
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string estado = "Todas",
        string busqueda = "",
        int pagina = 1,
        int? panelId = null,
        CancellationToken cancellationToken = default)
    {
        const string todas = "Todas";
        var estadosPermitidos = new[] { todas, "Enviada", "Vencida", "Aprobada" };
        if (!estadosPermitidos.Contains(estado, StringComparer.OrdinalIgnoreCase))
        {
            estado = todas;
        }
        else
        {
            estado = estadosPermitidos.First(value => value.Equals(estado, StringComparison.OrdinalIgnoreCase));
        }

        var ahora = DateTime.UtcNow;
        var cotizacionesEmitidas = await _context.Cotizaciones
            .AsNoTracking()
            .Where(c => c.Estado == "Enviada" || c.Estado == "Aprobada")
            .OrderByDescending(c => c.FechaEmision)
            .ThenByDescending(c => c.Id)
            .Select(c => new CotizacionBandejaItemViewModel
            {
                Id = c.Id,
                Correlativo = c.Correlativo,
                Cliente = c.Cliente.RazonSocial,
                Ruc = c.Cliente.Ruc,
                FechaEmision = c.FechaEmision,
                FechaVencimiento = c.FechaVencimiento ?? c.FechaEmision.AddHours(48),
                Total = c.Total,
                Estado = c.Estado,
                NumeroOrdenCompraCliente = c.NumeroOrdenCompraCliente,
                NombreArchivoOrdenCompraCliente = c.NombreArchivoOrdenCompraCliente,
                PuedeReemplazarOrdenCompra = c.Estado == "Aprobada"
                    && c.RutaOrdenCompraCliente != null
                    && (c.OrdenVenta == null || !c.OrdenVenta.OrdenesCompra.Any()),
                Detalles = c.Detalles
                    .OrderBy(d => d.Id)
                    .Select(d => new CotizacionBandejaDetalleViewModel
                    {
                        Sku = d.Producto.Sku,
                        Producto = d.Producto.Nombre,
                        Cantidad = d.Cantidad,
                        PrecioUnitario = d.PrecioVentaCalculado,
                        Subtotal = d.Subtotal
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var cotizacion in cotizacionesEmitidas)
        {
            if (cotizacion.Estado == "Enviada" && cotizacion.FechaVencimiento <= ahora)
            {
                cotizacion.Estado = "Vencida";
            }
        }

        var coincidenciasBusqueda = string.IsNullOrWhiteSpace(busqueda)
            ? cotizacionesEmitidas
            : cotizacionesEmitidas.Where(c =>
                c.Correlativo.Contains(busqueda.Trim(), StringComparison.OrdinalIgnoreCase)
                || c.Cliente.Contains(busqueda.Trim(), StringComparison.OrdinalIgnoreCase)
                || c.Ruc.Contains(busqueda.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var cotizacionesFiltradas = estado.Equals(todas, StringComparison.OrdinalIgnoreCase)
            ? coincidenciasBusqueda
            : coincidenciasBusqueda.Where(c => c.Estado.Equals(estado, StringComparison.OrdinalIgnoreCase)).ToList();

        var totalPaginas = Math.Max(1, (int)Math.Ceiling(cotizacionesFiltradas.Count / (double)CotizacionesPorPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);
        var model = new CotizacionBandejaViewModel
        {
            EstadoFiltro = estado,
            Busqueda = busqueda.Trim(),
            PanelAbiertoId = panelId,
            Pagina = pagina,
            TotalPaginas = totalPaginas,
            TotalResultados = cotizacionesFiltradas.Count,
            ResultadosTodas = coincidenciasBusqueda.Count,
            ResultadosEnviadas = coincidenciasBusqueda.Count(c => c.Estado == "Enviada"),
            ResultadosVencidas = coincidenciasBusqueda.Count(c => c.Estado == "Vencida"),
            ResultadosAprobadas = coincidenciasBusqueda.Count(c => c.Estado == "Aprobada"),
            Cotizaciones = cotizacionesFiltradas
                .Skip((pagina - 1) * CotizacionesPorPagina)
                .Take(CotizacionesPorPagina)
                .ToList()
        };

        ViewData["Title"] = "Cotizaciones";
        return View("Index", model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int clienteId, int? cotizacionId, CancellationToken cancellationToken)
    {
        if (cotizacionId.HasValue)
        {
            var cotizacionExistente = await _context.Cotizaciones
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cotizacionId.Value && c.ClienteId == clienteId, cancellationToken);
            if (cotizacionExistente == null)
            {
                return NotFound();
            }

            if (cotizacionExistente.Estado == "Aprobada")
            {
                return RedirectToAction(nameof(Index), new { estado = "Aprobada" });
            }
        }

        var model = await BuildModelAsync(clienteId, cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        if (cotizacionId.HasValue)
        {
            var cotizacion = await _context.Cotizaciones
                .AsNoTracking()
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(c => c.Id == cotizacionId.Value && c.ClienteId == clienteId, cancellationToken);
            if (cotizacion == null)
            {
                return NotFound();
            }

            model.CotizacionId = cotizacion.Id;
            model.Correlativo = cotizacion.Correlativo;
            model.FechaEmision = cotizacion.FechaEmision;
            model.EstadoCotizacion = cotizacion.Estado;
            model.CondicionPago = cotizacion.CondicionPago;
            model.Subtotal = cotizacion.Subtotal;
            model.Igv = cotizacion.Igv;
            model.Total = cotizacion.Total;
            model.ProductosCotizacionGuardada = cotizacion.Detalles
                .OrderBy(d => d.Id)
                .Select(d => new ProductoCotizacionGuardadaViewModel
                {
                    ProductoId = d.ProductoId,
                    Sku = d.Producto.Sku,
                    Nombre = d.Producto.Nombre,
                    UnidadMedida = d.Producto.UnidadMedida,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioVentaCalculado,
                    Subtotal = d.Subtotal,
                    RutaFichaTecnicaPdf = d.Producto.RutaFichaTecnicaPdf,
                    NombreArchivoPdf = d.Producto.NombreArchivoPdf
                })
                .ToList();

            foreach (var producto in model.ProductosDisponibles)
            {
                var detalle = cotizacion.Detalles.FirstOrDefault(d => d.ProductoId == producto.ProductoId);
                if (detalle == null)
                {
                    continue;
                }

                producto.Seleccionado = true;
                producto.Cantidad = detalle.Cantidad;
                producto.PrecioUnitario = detalle.PrecioVentaCalculado;
                producto.MargenPorcentaje = detalle.MargenDeseado * 100m;
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CotizacionClienteViewModel model, CancellationToken cancellationToken)
    {
        if (model.CotizacionId.HasValue)
        {
            var cotizacionExistente = await _context.Cotizaciones
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == model.CotizacionId.Value, cancellationToken);
            if (cotizacionExistente == null)
            {
                return NotFound();
            }

            TempData["ErrorMessage"] = cotizacionExistente.Estado == "Aprobada"
                ? "Una cotización aprobada es de solo lectura y no puede modificarse."
                : "La cotización indicada no se puede modificar desde este formulario.";
            return RedirectToAction(nameof(Index), new { estado = cotizacionExistente.Estado });
        }

        var current = await BuildModelAsync(model.ClienteId, cancellationToken);
        if (current == null)
        {
            return NotFound();
        }

        var productosEnviados = model.ProductosDisponibles ?? new();
        foreach (var producto in current.ProductosDisponibles)
        {
            var enviado = productosEnviados.FirstOrDefault(p => p.ProductoId == producto.ProductoId);
            if (enviado == null)
            {
                continue;
            }

            producto.Seleccionado = enviado.Seleccionado;
            producto.Cantidad = enviado.Cantidad;
            producto.MargenPorcentaje = enviado.MargenPorcentaje;
            producto.PrecioUnitario = enviado.PrecioUnitario > 0 ? enviado.PrecioUnitario : producto.CostoReferencial;
        }

        if (!CondicionPagoRules.EsPlazoCreditoValido(model.CondicionPago))
        {
            ModelState.AddModelError(nameof(model.CondicionPago), "Seleccione un plazo de crédito válido: 7, 15 o 30 días.");
            current.CondicionPago = CondicionPagoRules.PlazoCreditoPredeterminado;
        }
        else
        {
            current.CondicionPago = model.CondicionPago.Trim();
        }

        var noPuedeVender = !ClienteBusquedaRules.PuedeAbrirCotizacion(current.EstadoSunat, current.CondicionSunat);

        if (noPuedeVender)
        {
            ModelState.AddModelError(string.Empty, "No se puede generar la cotización: el cliente no figura ACTIVO y HABIDO en SUNAT.");
        }
        else if (current.DatosSunatDesactualizados)
        {
            ModelState.AddModelError(string.Empty, "Debe actualizar los datos fiscales del cliente según SUNAT antes de generar el documento de venta.");
        }
        else if (current.ClienteBloqueado)
        {
            ModelState.AddModelError(string.Empty, $"Cliente Bloqueado por Mora: no se permiten cotizaciones a crédito. {current.MotivoBloqueo}");
        }

        var productosSeleccionados = productosEnviados.Where(p => p.Seleccionado).ToList();
        if (productosSeleccionados.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Selecciona al menos un producto del catálogo.");
        }

        var idsSeleccionados = productosSeleccionados.Select(p => p.ProductoId).ToList();
        if (idsSeleccionados.Count != idsSeleccionados.Distinct().Count())
        {
            ModelState.AddModelError(string.Empty, "No se puede agregar el mismo producto más de una vez.");
        }

        var productosActivos = await _context.Productos
            .Where(p => p.Activo && idsSeleccionados.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (productosActivos.Count != idsSeleccionados.Distinct().Count())
        {
            ModelState.AddModelError(string.Empty, "Uno o más productos seleccionados ya no están disponibles en el catálogo.");
        }

        var detalles = new List<CotizacionDetalle>();
        foreach (var seleccionado in productosSeleccionados)
        {
            var indice = productosEnviados.IndexOf(seleccionado);
            if (seleccionado.Cantidad < 1)
            {
                ModelState.AddModelError($"ProductosDisponibles[{indice}].Cantidad", "La cantidad debe ser un número entero mayor a cero.");
            }

            if (seleccionado.MargenPorcentaje is < 0 or > 1000)
            {
                ModelState.AddModelError($"ProductosDisponibles[{indice}].MargenPorcentaje", "El margen debe estar entre 0 y 1000 %.");
            }

            if (!productosActivos.TryGetValue(seleccionado.ProductoId, out var producto)
                || seleccionado.Cantidad < 1)
            {
                continue;
            }

            var precioUnitario = ModelState.ContainsKey($"ProductosDisponibles[{indice}].PrecioUnitario")
                ? seleccionado.PrecioUnitario : producto.CostoReferencial;
            if (!CotizacionPrecioRules.EsPrecioValido(precioUnitario, producto.CostoReferencial))
            {
                ModelState.AddModelError(string.Empty, CotizacionPrecioRules.MensajePrecioMenorACosto);
                precioUnitario = producto.CostoReferencial;
            }

            var subtotalLinea = CotizacionPrecioRules.CalcularSubtotalItem(seleccionado.Cantidad, precioUnitario);
            var margenDeseado = producto.CostoReferencial > 0
                ? (precioUnitario - producto.CostoReferencial) / producto.CostoReferencial
                : 0m;

            detalles.Add(new CotizacionDetalle
            {
                ProductoId = producto.Id,
                Cantidad = seleccionado.Cantidad,
                CostoProveedorReferencial = producto.CostoReferencial,
                MargenDeseado = Math.Round(margenDeseado, 4, MidpointRounding.AwayFromZero),
                PrecioVentaCalculado = precioUnitario,
                Subtotal = subtotalLinea
            });
        }

        var subtotal = detalles.Sum(d => d.Subtotal);
        var igv = CotizacionPrecioRules.CalcularIgv(subtotal);
        var total = CotizacionPrecioRules.CalcularTotal(subtotal, igv);
        current.Subtotal = subtotal;
        current.Igv = igv;
        current.Total = total;

        if (!ModelState.IsValid)
        {
            ReordenarEstadoProductos(current, productosEnviados);
            ViewData["CotizacionPostInvalido"] = true;
            return View(current);
        }

        var fechaEmision = DateTime.UtcNow;
        var cotizacion = new Cotizacion
        {
            ClienteId = current.ClienteId,
            FechaEmision = fechaEmision,
            CondicionPago = current.CondicionPago,
            Subtotal = subtotal,
            Igv = igv,
            Total = total,
            Estado = "Borrador",
            Detalles = detalles
        };

        await GuardarCotizacionConCorrelativoAsync(cotizacion, fechaEmision, cancellationToken);
        TempData["SuccessMessage"] = CotizacionPrecioRules.MensajeGuardadoExitoso;
        TempData["CotizacionGuardada"] = true;
        return RedirectToAction(nameof(Create), new { clienteId = current.ClienteId, cotizacionId = cotizacion.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DescargarPdf(int cotizacionId, CancellationToken cancellationToken)
    {
        var cotizacion = await _context.Cotizaciones
            .Include(c => c.Cliente)
            .Include(c => c.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(c => c.Id == cotizacionId, cancellationToken);
        if (cotizacion == null)
        {
            return NotFound();
        }

        if (cotizacion.Detalles.Count == 0)
        {
            return BadRequest("La cotización no contiene productos para generar el PDF.");
        }

        var emitirPorPrimeraVez = string.Equals(cotizacion.Estado, "Borrador", StringComparison.Ordinal);
        if (emitirPorPrimeraVez)
        {
            var fechaEmision = DateTime.UtcNow;
            cotizacion.Estado = "Enviada";
            cotizacion.FechaEmision = fechaEmision;
            cotizacion.FechaVencimiento = fechaEmision.AddHours(48);
        }

        var pdf = GenerarPdf(cotizacion);
        if (emitirPorPrimeraVez)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        var fileName = NombreArchivoPdf(cotizacion);
        return File(pdf, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> VerPdf(int id, CancellationToken cancellationToken)
    {
        var cotizacion = await ObtenerCotizacionPdfAsync(id, cancellationToken);
        if (cotizacion == null)
        {
            return NotFound();
        }

        if (cotizacion.Detalles.Count == 0)
        {
            return BadRequest("La cotización no contiene productos para generar el PDF.");
        }

        var contentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = NombreArchivoPdf(cotizacion)
        };
        Response.Headers.ContentDisposition = contentDisposition.ToString();
        return File(GenerarPdf(cotizacion), "application/pdf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(OrdenCompraPdfValidator.TamanoMaximoBytes + 1024 * 1024)]
    public async Task<IActionResult> ConvertirAVenta(
        int id,
        string? numeroOrdenCompraCliente,
        IFormFile? archivoOrdenCompra,
        CancellationToken cancellationToken)
    {
        var cotizacion = await _context.Cotizaciones
            .Include(c => c.OrdenVenta)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cotizacion == null)
        {
            return NotFound();
        }

        var fechaVencimiento = cotizacion.FechaVencimiento ?? cotizacion.FechaEmision.AddHours(48);
        if (cotizacion.Estado != "Enviada" || fechaVencimiento <= DateTime.UtcNow)
        {
            TempData["ErrorMessage"] = "Solo se puede convertir una cotización Enviada y vigente.";
            return RedirectToAction(nameof(Index));
        }

        if (cotizacion.OrdenVenta != null)
        {
            TempData["ErrorMessage"] = "La cotización ya fue registrada como venta.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(numeroOrdenCompraCliente)
            || numeroOrdenCompraCliente.Trim().Length > 100)
        {
            TempData["ErrorMessage"] = "Ingresa un número de Orden de Compra de referencia válido (máximo 100 caracteres).";
            return RedirectToAction(nameof(Index), new { panelId = id });
        }

        var errorArchivo = await ValidarOrdenCompraAsync(archivoOrdenCompra, cancellationToken);
        if (errorArchivo != null)
        {
            TempData["ErrorMessage"] = errorArchivo;
            return RedirectToAction(nameof(Index), new { panelId = id });
        }

        var archivoGuardado = await GuardarOrdenCompraAsync(archivoOrdenCompra!, id, cancellationToken);
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            cotizacion.Estado = "Aprobada";
            cotizacion.NumeroOrdenCompraCliente = numeroOrdenCompraCliente.Trim();
            cotizacion.RutaOrdenCompraCliente = archivoGuardado.NombreAlmacenado;
            cotizacion.NombreArchivoOrdenCompraCliente = ObtenerNombreArchivo(archivoOrdenCompra!.FileName);
            _context.OrdenesVenta.Add(new OrdenVenta { CotizacionId = cotizacion.Id });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            EliminarArchivoSiExiste(archivoGuardado.RutaFisica, id);
            throw;
        }

        TempData["SuccessMessage"] = $"La cotización {cotizacion.Correlativo} fue aprobada y registrada como venta.";
        return RedirectToAction(nameof(Index), new { estado = "Aprobada" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(OrdenCompraPdfValidator.TamanoMaximoBytes + 1024 * 1024)]
    public async Task<IActionResult> ReemplazarOrdenCompra(
        int id,
        string? numeroOrdenCompraCliente,
        IFormFile? archivoOrdenCompra,
        CancellationToken cancellationToken)
    {
        var cotizacion = await _context.Cotizaciones
            .Include(c => c.OrdenVenta)
                .ThenInclude(o => o!.OrdenesCompra)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cotizacion == null)
        {
            return NotFound();
        }

        if (cotizacion.Estado != "Aprobada"
            || string.IsNullOrWhiteSpace(cotizacion.RutaOrdenCompraCliente)
            || cotizacion.OrdenVenta?.OrdenesCompra.Count > 0)
        {
            TempData["ErrorMessage"] = "La Orden de Compra solo puede reemplazarse en una venta aprobada que aún no tenga órdenes de compra a proveedores.";
            return RedirectToAction(nameof(Index));
        }

        if (!string.IsNullOrWhiteSpace(numeroOrdenCompraCliente)
            && numeroOrdenCompraCliente.Trim().Length > 100)
        {
            TempData["ErrorMessage"] = "El número de Orden de Compra no puede superar los 100 caracteres.";
            return RedirectToAction(nameof(Index), new { panelId = id });
        }

        var errorArchivo = await ValidarOrdenCompraAsync(archivoOrdenCompra, cancellationToken);
        if (errorArchivo != null)
        {
            TempData["ErrorMessage"] = errorArchivo;
            return RedirectToAction(nameof(Index), new { panelId = id });
        }

        var archivoAnterior = cotizacion.RutaOrdenCompraCliente;
        var archivoGuardado = await GuardarOrdenCompraAsync(archivoOrdenCompra!, id, cancellationToken);
        try
        {
            cotizacion.RutaOrdenCompraCliente = archivoGuardado.NombreAlmacenado;
            cotizacion.NombreArchivoOrdenCompraCliente = ObtenerNombreArchivo(archivoOrdenCompra!.FileName);
            if (!string.IsNullOrWhiteSpace(numeroOrdenCompraCliente))
            {
                cotizacion.NumeroOrdenCompraCliente = numeroOrdenCompraCliente.Trim();
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            EliminarArchivoSiExiste(archivoGuardado.RutaFisica, id);
            throw;
        }

        EliminarArchivoAnterior(archivoAnterior, id);
        TempData["SuccessMessage"] = $"La Orden de Compra de {cotizacion.Correlativo} fue reemplazada.";
        return RedirectToAction(nameof(Index), new { estado = "Aprobada" });
    }

    [HttpGet]
    public async Task<IActionResult> VerOrdenCompra(int id, CancellationToken cancellationToken)
    {
        var cotizacion = await _context.Cotizaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.Estado == "Aprobada", cancellationToken);
        if (cotizacion == null || string.IsNullOrWhiteSpace(cotizacion.RutaOrdenCompraCliente))
        {
            return NotFound();
        }

        var nombreAlmacenado = Path.GetFileName(cotizacion.RutaOrdenCompraCliente);
        if (!string.Equals(nombreAlmacenado, cotizacion.RutaOrdenCompraCliente, StringComparison.Ordinal)
            || !string.Equals(Path.GetExtension(nombreAlmacenado), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("La ruta de la Orden de Compra de la cotización {CotizacionId} no es válida.", id);
            return NotFound();
        }

        var rutaFisica = Path.Combine(ObtenerDirectorioOrdenesCompra(), nombreAlmacenado);
        if (!System.IO.File.Exists(rutaFisica))
        {
            _logger.LogError("No se encontró el archivo de Orden de Compra para la cotización {CotizacionId}.", id);
            return NotFound();
        }

        var nombreDescarga = ObtenerNombreArchivo(cotizacion.NombreArchivoOrdenCompraCliente ?? nombreAlmacenado);
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = nombreDescarga
        }.ToString();
        return File(System.IO.File.OpenRead(rutaFisica), "application/pdf");
    }

    private async Task<string?> ValidarOrdenCompraAsync(
        IFormFile? archivo,
        CancellationToken cancellationToken)
    {
        if (archivo == null)
        {
            return "Adjunta la Orden de Compra del cliente en formato PDF.";
        }

        await using var stream = archivo.OpenReadStream();
        return await OrdenCompraPdfValidator.ValidarAsync(
            archivo.FileName,
            archivo.Length,
            stream,
            cancellationToken);
    }

    private async Task<(string NombreAlmacenado, string RutaFisica)> GuardarOrdenCompraAsync(
        IFormFile archivo,
        int cotizacionId,
        CancellationToken cancellationToken)
    {
        var nombreAlmacenado = $"{Guid.NewGuid():N}.pdf";
        var rutaFisica = Path.Combine(ObtenerDirectorioOrdenesCompra(), nombreAlmacenado);
        Directory.CreateDirectory(Path.GetDirectoryName(rutaFisica)!);

        try
        {
            await using var destino = new FileStream(rutaFisica, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await archivo.CopyToAsync(destino, cancellationToken);
            return (nombreAlmacenado, rutaFisica);
        }
        catch
        {
            EliminarArchivoSiExiste(rutaFisica, cotizacionId);
            throw;
        }
    }

    private string ObtenerDirectorioOrdenesCompra() =>
        Path.Combine(_env.ContentRootPath, "App_Data", "OrdenesCompraCliente");

    private static string ObtenerNombreArchivo(string nombreArchivo)
    {
        var nombreSeguro = Path.GetFileName(nombreArchivo);
        return nombreSeguro.Length <= 255 ? nombreSeguro : nombreSeguro[..255];
    }

    private void EliminarArchivoAnterior(string nombreAlmacenado, int cotizacionId)
    {
        var nombreSeguro = Path.GetFileName(nombreAlmacenado);
        if (!string.Equals(nombreSeguro, nombreAlmacenado, StringComparison.Ordinal))
        {
            _logger.LogError("La ruta anterior de la Orden de Compra de la cotización {CotizacionId} no es válida.", cotizacionId);
            return;
        }

        EliminarArchivoSiExiste(Path.Combine(ObtenerDirectorioOrdenesCompra(), nombreSeguro), cotizacionId);
    }

    private void EliminarArchivoSiExiste(string rutaFisica, int cotizacionId)
    {
        try
        {
            if (System.IO.File.Exists(rutaFisica))
            {
                System.IO.File.Delete(rutaFisica);
            }
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "No se pudo limpiar el archivo de Orden de Compra de la cotización {CotizacionId}.", cotizacionId);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "No se pudo limpiar el archivo de Orden de Compra de la cotización {CotizacionId}.", cotizacionId);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(int id, CancellationToken cancellationToken)
    {
        var cotizacionVencida = await _context.Cotizaciones
            .Include(c => c.Detalles)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cotizacionVencida == null)
        {
            return NotFound();
        }

        var ahora = DateTime.UtcNow;
        var fechaVencimiento = cotizacionVencida.FechaVencimiento ?? cotizacionVencida.FechaEmision.AddHours(48);
        if (cotizacionVencida.Estado != "Enviada" || fechaVencimiento > ahora)
        {
            TempData["ErrorMessage"] = "Solo se pueden actualizar cotizaciones vencidas.";
            return RedirectToAction(nameof(Index));
        }

        if (cotizacionVencida.Detalles.Count == 0)
        {
            TempData["ErrorMessage"] = "La cotización vencida no contiene productos para actualizar.";
            return RedirectToAction(nameof(Index));
        }

        var productoIds = cotizacionVencida.Detalles.Select(d => d.ProductoId).Distinct().ToList();
        var productosActuales = await _context.Productos
            .Where(p => p.Activo && productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        if (productosActuales.Count != productoIds.Count)
        {
            TempData["ErrorMessage"] = "No se pudo actualizar la cotización porque uno o más productos ya no están activos en el catálogo.";
            return RedirectToAction(nameof(Index), new { estado = "Vencida" });
        }

        var detalles = new List<CotizacionDetalle>();
        foreach (var detalleAnterior in cotizacionVencida.Detalles)
        {
            var producto = productosActuales[detalleAnterior.ProductoId];
            var precioActualizado = Math.Round(
                producto.CostoReferencial * (1m + detalleAnterior.MargenDeseado),
                2,
                MidpointRounding.AwayFromZero);
            if (!CotizacionPrecioRules.EsPrecioValido(precioActualizado, producto.CostoReferencial)
                || precioActualizado > 999999.99m)
            {
                TempData["ErrorMessage"] = $"El precio actualizado de {producto.Sku} excede el límite permitido. Ajusta el catálogo antes de renovar la cotización.";
                return RedirectToAction(nameof(Index), new { estado = "Vencida" });
            }

            detalles.Add(new CotizacionDetalle
            {
                ProductoId = producto.Id,
                Cantidad = detalleAnterior.Cantidad,
                CostoProveedorReferencial = producto.CostoReferencial,
                MargenDeseado = producto.CostoReferencial > 0m
                    ? Math.Round((precioActualizado - producto.CostoReferencial) / producto.CostoReferencial, 4, MidpointRounding.AwayFromZero)
                    : 0m,
                PrecioVentaCalculado = precioActualizado,
                Subtotal = CotizacionPrecioRules.CalcularSubtotalItem(detalleAnterior.Cantidad, precioActualizado)
            });
        }

        var subtotal = detalles.Sum(d => d.Subtotal);
        var igv = CotizacionPrecioRules.CalcularIgv(subtotal);
        var nuevaCotizacion = new Cotizacion
        {
            ClienteId = cotizacionVencida.ClienteId,
            FechaEmision = ahora,
            FechaVencimiento = ahora.AddHours(48),
            Estado = "Enviada",
            CondicionPago = cotizacionVencida.CondicionPago,
            Subtotal = subtotal,
            Igv = igv,
            Total = CotizacionPrecioRules.CalcularTotal(subtotal, igv),
            Detalles = detalles
        };

        await GuardarCotizacionConCorrelativoAsync(nuevaCotizacion, ahora, cancellationToken);
        TempData["SuccessMessage"] = $"Se creó la cotización {nuevaCotizacion.Correlativo} con vigencia de 48 horas.";
        return RedirectToAction(nameof(Index), new { estado = "Enviada" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarDesdeSunat(int clienteId, CancellationToken cancellationToken)
    {
        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId && c.Activo, cancellationToken);
        if (cliente == null)
        {
            return NotFound();
        }

        var resultado = await _sunatService.ConsultarRucAsync(cliente.Ruc, cancellationToken);
        if (!resultado.Success)
        {
            TempData["ErrorMessage"] = resultado.Message;
            return RedirectToAction(nameof(Create), new { clienteId });
        }

        cliente.RazonSocial = resultado.RazonSocial?.Trim().ToUpper() ?? cliente.RazonSocial;
        cliente.DireccionFiscal = resultado.DireccionFiscal?.Trim() ?? cliente.DireccionFiscal;
        cliente.Departamento = resultado.Departamento?.Trim();
        cliente.Provincia = resultado.Provincia?.Trim();
        cliente.Distrito = resultado.Distrito?.Trim();
        cliente.Ubigeo = resultado.Ubigeo?.Trim();
        cliente.EstadoSunat = resultado.Estado?.Trim().ToUpper() ?? cliente.EstadoSunat;
        cliente.CondicionSunat = resultado.Condicion?.Trim().ToUpper() ?? cliente.CondicionSunat;
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Los datos fiscales fueron actualizados con la información más reciente de SUNAT.";
        return RedirectToAction(nameof(Create), new { clienteId });
    }

    private void ReordenarEstadoProductos(CotizacionClienteViewModel current, List<ProductoCotizacionViewModel> enviados)
    {
        // El catálogo puede cambiar entre GET y POST. Razor prioriza ModelState:
        // sus índices deben corresponder al catálogo actual, nunca a otra fila.
        const string prefijo = "ProductosDisponibles[";
        // Copiar los valores antes de modificar el diccionario: un nuevo índice
        // puede reutilizar el mismo ModelStateEntry que otra fila tenía antes.
        var estados = ModelState.Where(e => e.Key.StartsWith(prefijo, StringComparison.Ordinal))
            .Select(e => new
            {
                e.Key,
                RawValue = e.Value?.RawValue,
                AttemptedValue = e.Value?.AttemptedValue,
                Errores = e.Value?.Errors.Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                    ? "El valor ingresado para el producto no es válido." : error.ErrorMessage).ToArray() ?? Array.Empty<string>()
            }).ToList();
        foreach (var estado in estados) ModelState.Remove(estado.Key);

        foreach (var estado in estados)
        {
            var cierre = estado.Key.IndexOf(']');
            var indiceActual = -1;
            if (cierre > prefijo.Length && int.TryParse(estado.Key[prefijo.Length..cierre], out var indiceEnviado)
                && indiceEnviado >= 0 && indiceEnviado < enviados.Count)
            {
                indiceActual = current.ProductosDisponibles.FindIndex(p => p.ProductoId == enviados[indiceEnviado].ProductoId);
            }

            var campo = cierre >= 0 ? estado.Key[(cierre + 1)..] : "";
            var clave = indiceActual >= 0 && campo is ".Cantidad" or ".MargenPorcentaje" or ".Seleccionado" or ".PrecioUnitario"
                ? $"ProductosDisponibles[{indiceActual}]{campo}" : string.Empty;
            if (clave.Length > 0)
                ModelState.SetModelValue(clave, estado.RawValue, estado.AttemptedValue);

            foreach (var error in estado.Errores) ModelState.AddModelError(clave, error);
        }
    }

    private async Task<CotizacionClienteViewModel?> BuildModelAsync(int clienteId, CancellationToken cancellationToken)
    {
        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteId && c.Activo, cancellationToken);
        if (cliente == null)
        {
            return null;
        }

        var facturas = await _context.Facturas
            .AsNoTracking()
            .Where(f => f.ClienteId == clienteId && f.EstadoPago != "Pagado")
            .ToListAsync(cancellationToken);
        var ahora = DateTime.UtcNow;
        var vencidas = facturas.Where(f => f.FechaVencimiento < ahora).ToList();
        var saldoPendiente = facturas.Sum(f => f.Total);
        var saldoVencido = vencidas.Sum(f => f.Total);
        var diasMora = vencidas.Count == 0
            ? 0
            : vencidas.Max(f => Math.Max(0, (ahora.Date - f.FechaVencimiento.Date).Days));
        var bloqueado = vencidas.Count > 0;
        var resultadoSunat = await _sunatService.ConsultarRucAsync(cliente.Ruc, cancellationToken);
        var datosSunatDesactualizados = resultadoSunat.Success
            && (!string.IsNullOrWhiteSpace(resultadoSunat.DireccionFiscal)
                && !string.Equals(cliente.DireccionFiscal.Trim(), resultadoSunat.DireccionFiscal.Trim(), StringComparison.OrdinalIgnoreCase));
        var estadoSunat = resultadoSunat.Success ? resultadoSunat.Estado ?? cliente.EstadoSunat : cliente.EstadoSunat;
        var condicionSunat = resultadoSunat.Success ? resultadoSunat.Condicion ?? cliente.CondicionSunat : cliente.CondicionSunat;

        var model = new CotizacionClienteViewModel
        {
            ClienteId = cliente.Id,
            Ruc = cliente.Ruc,
            RazonSocial = cliente.RazonSocial,
            DireccionFiscal = cliente.DireccionFiscal,
            EstadoSunat = estadoSunat,
            CondicionSunat = condicionSunat,
            SaldoPendiente = saldoPendiente,
            SaldoVencido = saldoVencido,
            DiasMora = diasMora,
            ClienteBloqueado = bloqueado,
            DatosSunatDesactualizados = datosSunatDesactualizados,
            MotivoBloqueo = vencidas.Count > 0
                ? $"Tiene facturas vencidas hasta {diasMora} días."
                : null
        };

        model.ProductosDisponibles = await _context.Productos
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new ProductoCotizacionViewModel
            {
                ProductoId = p.Id,
                Sku = p.Sku,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                UnidadMedida = p.UnidadMedida,
                CostoReferencial = p.CostoReferencial,
                PrecioUnitario = p.CostoReferencial,
                StockDisponible = p.StockDisponible,
                RutaImagen = p.RutaImagen ?? string.Empty,
                RutaFichaTecnicaPdf = p.RutaFichaTecnicaPdf,
                NombreArchivoPdf = p.NombreArchivoPdf
            })
            .ToListAsync(cancellationToken);

        return model;
    }

    private async Task GuardarCotizacionConCorrelativoAsync(
        Cotizacion cotizacion,
        DateTime fechaEmision,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _context.Cotizaciones.Add(cotizacion);
        await _context.SaveChangesAsync(cancellationToken);
        cotizacion.Correlativo = $"COT-{fechaEmision:yyyy}-{cotizacion.Id:D4}";
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private byte[] GenerarPdf(Cotizacion cotizacion) => CotizacionPdfGenerator.Generate(
        cotizacion,
        productoId => Url.Action("VerFicha", "Catalogo", new { id = productoId }, Request.Scheme),
        logoPath: Path.Combine(_env.WebRootPath, "logo_horizontal.png"));

    private async Task<Cotizacion?> ObtenerCotizacionPdfAsync(int id, CancellationToken cancellationToken) =>
        await _context.Cotizaciones
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Include(c => c.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    private static string NombreArchivoPdf(Cotizacion cotizacion) =>
        $"{SanitizeFileName(cotizacion.Correlativo)}_{SanitizeFileName(cotizacion.Cliente.RazonSocial)}.pdf";

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
        var safeName = new string(value
            .Select(character => invalidCharacters.Contains(character) || char.IsControl(character) ? '_' : character)
            .ToArray())
            .Trim()
            .Trim('.');
        return string.IsNullOrWhiteSpace(safeName) ? "Cliente" : safeName;
    }
}
