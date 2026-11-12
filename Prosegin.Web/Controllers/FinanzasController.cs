using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.Services.Nubefact;
using Prosegin.Web.ViewModels.Finanzas;

namespace Prosegin.Web.Controllers;

public class FinanzasController : Controller
{
    private const int RegistrosPorPagina = 5;
    private readonly ProseginDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public FinanzasController(ProseginDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> CuentasPorCobrar(
        int? clienteId,
        string? estado,
        DateTime? fechaEmisionDesde,
        DateTime? fechaEmisionHasta,
        DateTime? fechaVencimientoDesde,
        DateTime? fechaVencimientoHasta,
        int pagina = 1,
        CancellationToken cancellationToken = default)
    {
        var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? "TODAS" : estado.Trim().ToUpperInvariant();
        if (estadoFiltro is not ("AL_DIA" or "VENCIDA" or "PAGADA" or "TODAS"))
            estadoFiltro = "TODAS";

        var model = new CuentasPorCobrarViewModel
        {
            ClienteId = clienteId,
            Estado = estadoFiltro,
            FechaEmisionDesde = fechaEmisionDesde,
            FechaEmisionHasta = fechaEmisionHasta,
            FechaVencimientoDesde = fechaVencimientoDesde,
            FechaVencimientoHasta = fechaVencimientoHasta,
            Clientes = await _context.Clientes
                .AsNoTracking()
                .Where(cliente => cliente.Facturas.Any())
                .OrderBy(cliente => cliente.RazonSocial)
                .Select(cliente => new ClienteFiltroFacturaViewModel
                {
                    Id = cliente.Id,
                    Nombre = cliente.RazonSocial
                })
                .ToListAsync(cancellationToken)
        };

        if (FechasInvalidas(fechaEmisionDesde, fechaEmisionHasta)
            || FechasInvalidas(fechaVencimientoDesde, fechaVencimientoHasta))
        {
            model.Mensaje = "La fecha inicial no puede ser mayor a la fecha final";
            return View(model);
        }

        var registros = await _context.Facturas
            .AsNoTracking()
            .Include(factura => factura.Cliente)
            .Include(factura => factura.Pagos)
            .Include(factura => factura.Detalles)
            .Where(factura => factura.EstadoEmision != NubefactService.EstadoRechazado)
            .ToListAsync(cancellationToken);
        var hoy = DateTime.Today;
        var facturas = registros.Select(factura =>
            {
                var saldo = CalcularSaldoPendiente(factura);
                var abonado = Math.Max(0, factura.Total - saldo);
                return new CuentaPorCobrarItemViewModel
                {
                    Id = factura.Id,
                    ClienteId = factura.ClienteId,
                    NumeroFactura = factura.NumeroFactura,
                    Cliente = factura.Cliente.RazonSocial,
                    RucCliente = factura.Cliente.Ruc,
                    FechaEmision = factura.FechaEmision,
                    FechaVencimiento = factura.FechaVencimiento,
                    PlazoDias = Math.Max(0, (factura.FechaVencimiento.Date - factura.FechaEmision.Date).Days),
                    MontoTotal = factura.Total,
                    MontoAbonado = abonado,
                    SaldoPendiente = saldo,
                    Estado = EstadoCuenta(factura, hoy),
                    PdfDisponible = !string.IsNullOrWhiteSpace(factura.EnlacePdf),
                    Detalles = factura.Detalles.Select(detalle => new DetalleFacturaCuentaViewModel
                    {
                        Descripcion = detalle.Descripcion,
                        Cantidad = detalle.Cantidad,
                        PrecioUnitario = detalle.PrecioUnitario,
                        Total = detalle.Total
                    }).ToList()
                };
            })
            .Where(factura => !clienteId.HasValue || factura.ClienteId == clienteId.Value)
            .Where(factura => !fechaEmisionDesde.HasValue || factura.FechaEmision.Date >= fechaEmisionDesde.Value.Date)
            .Where(factura => !fechaEmisionHasta.HasValue || factura.FechaEmision.Date <= fechaEmisionHasta.Value.Date)
            .Where(factura => !fechaVencimientoDesde.HasValue || factura.FechaVencimiento.Date >= fechaVencimientoDesde.Value.Date)
            .Where(factura => !fechaVencimientoHasta.HasValue || factura.FechaVencimiento.Date <= fechaVencimientoHasta.Value.Date)
            .Where(factura => estadoFiltro switch
            {
                "AL_DIA" => factura.Estado == "Al día",
                "VENCIDA" => factura.Estado == "Vencida",
                "PAGADA" => factura.Estado == "Pagada",
                _ => true
            })
            .OrderBy(factura => factura.FechaVencimiento)
            .ThenBy(factura => factura.NumeroFactura, StringComparer.OrdinalIgnoreCase)
            .ToList();

        model.TotalRegistros = facturas.Count;
        if (facturas.Count == 0)
        {
            model.Mensaje = "No se encontraron facturas con los criterios seleccionados";
            return View(model);
        }

        model.TotalPaginas = Math.Max(1, (int)Math.Ceiling(facturas.Count / (double)RegistrosPorPagina));
        model.Pagina = Math.Clamp(pagina, 1, model.TotalPaginas);
        model.Facturas = facturas.Skip((model.Pagina - 1) * RegistrosPorPagina)
            .Take(RegistrosPorPagina)
            .ToList();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DetalleFactura(int id, CancellationToken cancellationToken)
    {
        var factura = await _context.Facturas
            .AsNoTracking()
            .Include(item => item.Cliente)
            .Include(item => item.Pagos)
            .Include(item => item.Detalles)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (factura == null)
            return NotFound();

        var hoy = DateTime.Today;
        var saldo = CalcularSaldoPendiente(factura);
        var model = new DetalleCuentaPorCobrarViewModel
        {
            Id = factura.Id,
            NumeroFactura = factura.NumeroFactura,
            Cliente = factura.Cliente.RazonSocial,
            RucCliente = factura.Cliente.Ruc,
            FechaEmision = factura.FechaEmision,
            FechaVencimiento = factura.FechaVencimiento,
            PlazoDias = Math.Max(0, (factura.FechaVencimiento.Date - factura.FechaEmision.Date).Days),
            MontoTotal = factura.Total,
            SaldoPendiente = saldo,
            Estado = EstadoCuenta(factura, hoy),
            Detalles = factura.Detalles.Select(detalle => new DetalleFacturaCuentaViewModel
            {
                Descripcion = detalle.Descripcion,
                Cantidad = detalle.Cantidad,
                PrecioUnitario = detalle.PrecioUnitario,
                Total = detalle.Total
            }).ToList()
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> FacturasPorVencer(
        int? clienteId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        int pagina = 1,
        CancellationToken cancellationToken = default)
    {
        var hoy = DateTime.Today;
        var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? "POR_VENCER" : estado.Trim().ToUpperInvariant();
        if (estadoFiltro is not ("POR_VENCER" or "VENCIDA" or "PAGADA" or "TODAS"))
            estadoFiltro = "POR_VENCER";

        var model = new FacturasPorVencerViewModel
        {
            ClienteId = clienteId,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            Estado = estadoFiltro,
            Clientes = await _context.Clientes
                .AsNoTracking()
                .Where(cliente => cliente.Facturas.Any())
                .OrderBy(cliente => cliente.RazonSocial)
                .Select(cliente => new ClienteFiltroFacturaViewModel
                {
                    Id = cliente.Id,
                    Nombre = cliente.RazonSocial
                })
                .ToListAsync(cancellationToken)
        };

        if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date)
        {
            model.Mensaje = "La fecha inicial no puede ser mayor a la fecha final";
            return View(model);
        }

        var registrosFactura = await _context.Facturas
            .AsNoTracking()
            .Include(factura => factura.Cliente)
            .Include(factura => factura.Pagos)
            .Where(factura => factura.EstadoEmision != NubefactService.EstadoRechazado)
            .ToListAsync(cancellationToken);

        var facturas = registrosFactura
            .Select(factura =>
            {
                var saldoPendiente = CalcularSaldoPendiente(factura);
                return new FacturaPorVencerItemViewModel
                {
                    Id = factura.Id,
                    ClienteId = factura.ClienteId,
                    NumeroFactura = factura.NumeroFactura,
                    Cliente = factura.Cliente.RazonSocial,
                    FechaVencimiento = factura.FechaVencimiento,
                    Monto = saldoPendiente,
                    SaldoPendiente = saldoPendiente,
                    EstadoPago = factura.EstadoPago,
                    PdfDisponible = !string.IsNullOrWhiteSpace(factura.EnlacePdf)
                };
            })
            .ToList();

        model.PorVencerSieteDias = facturas.Count(factura =>
            !EsPagada(factura.EstadoPago) && DiasHasta(factura.FechaVencimiento, hoy) is >= 0 and <= 7);
        model.PorVencerQuinceDias = facturas.Count(factura =>
            !EsPagada(factura.EstadoPago) && DiasHasta(factura.FechaVencimiento, hoy) is >= 8 and <= 15);
        model.PorVencerTreintaDias = facturas.Count(factura =>
            !EsPagada(factura.EstadoPago) && DiasHasta(factura.FechaVencimiento, hoy) is >= 16 and <= 30);

        if (clienteId.HasValue)
            facturas = facturas.Where(factura => factura.ClienteId == clienteId.Value).ToList();

        if (fechaDesde.HasValue)
            facturas = facturas.Where(factura => factura.FechaVencimiento.Date >= fechaDesde.Value.Date).ToList();
        if (fechaHasta.HasValue)
            facturas = facturas.Where(factura => factura.FechaVencimiento.Date <= fechaHasta.Value.Date).ToList();

        facturas = estadoFiltro switch
        {
            "POR_VENCER" => facturas.Where(factura =>
                !EsPagada(factura.EstadoPago) && DiasHasta(factura.FechaVencimiento, hoy) is >= 0 and <= 30).ToList(),
            "VENCIDA" => facturas.Where(factura =>
                !EsPagada(factura.EstadoPago) && DiasHasta(factura.FechaVencimiento, hoy) < 0).ToList(),
            "PAGADA" => facturas.Where(factura => EsPagada(factura.EstadoPago)).ToList(),
            _ => facturas
        };

        facturas = facturas.OrderBy(factura => factura.FechaVencimiento)
            .ThenBy(factura => factura.NumeroFactura, StringComparer.OrdinalIgnoreCase)
            .ToList();
        model.TotalRegistros = facturas.Count;
        if (facturas.Count == 0)
        {
            model.Mensaje = clienteId.HasValue
                ? "No se encontraron facturas por vencer para el cliente seleccionado"
                : fechaDesde.HasValue || fechaHasta.HasValue
                    ? "No se encontraron facturas en el rango de fechas indicado"
                    : estadoFiltro != "POR_VENCER" && estadoFiltro != "TODAS"
                        ? "No se encontraron facturas con el estado seleccionado"
                        : "No hay facturas próximas a vencer";
            return View(model);
        }

        model.TotalPaginas = Math.Max(1, (int)Math.Ceiling(facturas.Count / (double)RegistrosPorPagina));
        model.Pagina = Math.Clamp(pagina, 1, model.TotalPaginas);
        model.Facturas = facturas.Skip((model.Pagina - 1) * RegistrosPorPagina)
            .Take(RegistrosPorPagina)
            .Select(factura =>
            {
                factura.DiasHastaVencimiento = DiasHasta(factura.FechaVencimiento, hoy);
                factura.EstadoPago = EstadoVisible(factura.EstadoPago, factura.FechaVencimiento, hoy);
                return factura;
            })
            .ToList();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DescargarFactura(
        int id,
        int? clienteId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        int pagina = 1,
        string? origen = null,
        int? clienteCuentasId = null,
        string? estadoCuentas = null,
        DateTime? emisionDesde = null,
        DateTime? emisionHasta = null,
        DateTime? vencimientoDesde = null,
        DateTime? vencimientoHasta = null,
        CancellationToken cancellationToken = default)
    {
        var enlacePdf = await _context.Facturas
            .AsNoTracking()
            .Where(factura => factura.Id == id)
            .Select(factura => factura.EnlacePdf)
            .FirstOrDefaultAsync(cancellationToken);
        if (!Uri.TryCreate(enlacePdf, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return PdfNoDisponible(clienteId, fechaDesde, fechaHasta, estado, pagina, origen,
                clienteCuentasId, estadoCuentas, emisionDesde, emisionHasta, vencimientoDesde, vencimientoHasta);

        try
        {
            using var response = await _httpClientFactory.CreateClient("ComprobantesClient").GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return PdfNoDisponible(clienteId, fechaDesde, fechaHasta, estado, pagina, origen,
                    clienteCuentasId, estadoCuentas, emisionDesde, emisionHasta, vencimientoDesde, vencimientoHasta);

            var contenido = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (contenido.Length == 0)
                return PdfNoDisponible(clienteId, fechaDesde, fechaHasta, estado, pagina, origen,
                    clienteCuentasId, estadoCuentas, emisionDesde, emisionHasta, vencimientoDesde, vencimientoHasta);

            var numeroFactura = await _context.Facturas.AsNoTracking()
                .Where(factura => factura.Id == id)
                .Select(factura => factura.NumeroFactura)
                .FirstAsync(cancellationToken);
            var nombreSeguro = new string(numeroFactura.Where(caracter => char.IsLetterOrDigit(caracter) || caracter is '-' or '_').ToArray());
            return File(contenido, "application/pdf", $"{(string.IsNullOrWhiteSpace(nombreSeguro) ? $"Factura-{id}" : nombreSeguro)}.pdf");
        }
        catch (HttpRequestException)
        {
            return PdfNoDisponible(clienteId, fechaDesde, fechaHasta, estado, pagina, origen,
                clienteCuentasId, estadoCuentas, emisionDesde, emisionHasta, vencimientoDesde, vencimientoHasta);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PdfNoDisponible(clienteId, fechaDesde, fechaHasta, estado, pagina, origen,
                clienteCuentasId, estadoCuentas, emisionDesde, emisionHasta, vencimientoDesde, vencimientoHasta);
        }
    }

    [HttpGet]
    public async Task<IActionResult> RegistroPago(
        string? busqueda,
        int pagina = 1,
        int? facturaId = null,
        CancellationToken cancellationToken = default)
    {
            const int tamanoPagina = 5;
            var termino = busqueda?.Trim() ?? string.Empty;
            var model = new RegistroPagosViewModel { Busqueda = termino, FacturaIdSeleccionada = facturaId };
            if (!string.IsNullOrEmpty(termino) && !BusquedaPagoValida(termino))
            {
                model.BusquedaInvalida = true;
                model.Mensaje = "Ingrese un número de factura o cliente válido";
                return View(model);
            }

            var hoy = DateTime.Today;
            var pendientes = await _context.Facturas
                .AsNoTracking()
                .Include(factura => factura.Cliente)
                .Include(factura => factura.Pagos)
                .Where(factura => factura.EstadoPago != "Pagado"
                    && factura.EstadoPago != "Pagada"
                    && factura.EstadoEmision != NubefactService.EstadoRechazado)
                .ToListAsync(cancellationToken);

            var facturas = pendientes
                .Select(factura =>
                {
                    var saldo = CalcularSaldoPendiente(factura);
                    var abonado = Math.Max(0, factura.Total - saldo);
                    var abonosOrdenados = factura.Pagos
                        .OrderBy(pago => pago.FechaPago)
                        .ThenBy(pago => pago.Id)
                        .Select((pago, idx) => new AbonoFacturaViewModel
                        {
                            NumeroCuota = idx + 1,
                            Monto = pago.Monto,
                            FechaPago = pago.FechaPago,
                            MetodoPago = pago.MetodoPago,
                            Referencia = pago.Referencia
                        })
                        .ToList();

                    return new FacturaPendientePagoViewModel
                    {
                        Id = factura.Id,
                        NumeroFactura = factura.NumeroFactura,
                        Cliente = factura.Cliente.RazonSocial,
                        RucCliente = factura.Cliente.Ruc,
                        FechaEmision = factura.FechaEmision,
                        FechaVencimiento = factura.FechaVencimiento,
                        PlazoDias = Math.Max(0, (factura.FechaVencimiento.Date - factura.FechaEmision.Date).Days),
                        MontoTotal = factura.Total,
                        MontoAbonado = abonado,
                        SaldoPendiente = saldo,
                        Estado = EstadoVisible(factura.EstadoPago, factura.FechaVencimiento, hoy),
                        CantidadAbonos = abonosOrdenados.Count,
                        Abonos = abonosOrdenados
                    };
                })
                .Where(factura => factura.SaldoPendiente > 0
                    && (string.IsNullOrEmpty(termino)
                        || factura.NumeroFactura.Contains(termino, StringComparison.OrdinalIgnoreCase)
                        || factura.Cliente.Contains(termino, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(factura => factura.FechaVencimiento)
                .ThenBy(factura => factura.NumeroFactura, StringComparer.OrdinalIgnoreCase)
                .ToList();

            model.TotalFacturas = facturas.Count;
            if (facturas.Count == 0)
            {
                model.Mensaje = string.IsNullOrEmpty(termino)
                    ? "No hay facturas pendientes de pago"
                    : "No se encontraron facturas pendientes que coincidan con la búsqueda";
                return View(model);
            }

            if (facturaId.HasValue)
            {
                var indice = facturas.FindIndex(factura => factura.Id == facturaId.Value);
                if (indice >= 0)
                {
                    model.FacturaSeleccionada = facturas[indice];
                    if (HttpContext?.Request.Query.ContainsKey("pagina") != true)
                        pagina = (indice / tamanoPagina) + 1;
                }
            }

            model.TotalPaginas = Math.Max(1, (int)Math.Ceiling(facturas.Count / (double)tamanoPagina));
            model.Pagina = Math.Clamp(pagina, 1, model.TotalPaginas);
            model.Facturas = facturas.Skip((model.Pagina - 1) * tamanoPagina).Take(tamanoPagina).ToList();
            return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPago(RegistrarPagoRequest request, CancellationToken cancellationToken)
    {
            var busqueda = request.Busqueda?.Trim();
            var pagina = Math.Max(1, request.Pagina);
            var facturaSeleccionada = request.FacturaId > 0 ? request.FacturaId : (int?)null;
            if (request.FacturaId <= 0)
                return PagoInvalido("Seleccione una factura pendiente de pago", busqueda, pagina);
            if (!request.Monto.HasValue || !request.FechaPago.HasValue || string.IsNullOrWhiteSpace(request.MetodoPago))
                return PagoInvalido("Complete los campos obligatorios", busqueda, pagina, facturaSeleccionada);
            if (request.Monto.Value == 0)
                return PagoInvalido("Ingrese un monto mayor a S/ 0.00", busqueda, pagina, facturaSeleccionada);
            if (request.Monto.Value < 0)
                return PagoInvalido("Ingrese un monto del pago válido", busqueda, pagina, facturaSeleccionada);
            if (request.FechaPago.Value.Date > DateTime.Today)
                return PagoInvalido("La fecha de pago no puede ser posterior a la fecha actual", busqueda, pagina, facturaSeleccionada);

            var metodoPago = new[] { "Transferencia", "Efectivo", "Tarjeta", "Depósito", "Cheque" }
                .FirstOrDefault(metodo => metodo.Equals(request.MetodoPago.Trim(), StringComparison.OrdinalIgnoreCase));
            if (metodoPago == null)
                return PagoInvalido("Complete los campos obligatorios", busqueda, pagina, facturaSeleccionada);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var factura = await _context.Facturas
                .Include(item => item.Pagos)
                .FirstOrDefaultAsync(item => item.Id == request.FacturaId, cancellationToken);
            if (factura == null)
                return PagoInvalido("Seleccione una factura pendiente de pago", busqueda, pagina);
            if (EsPagada(factura.EstadoPago))
                return PagoInvalido("La factura ya se encuentra pagada", busqueda, pagina);

            var saldoPendiente = CalcularSaldoPendiente(factura);
            if (saldoPendiente <= 0)
                return PagoInvalido("Seleccione una factura pendiente de pago", busqueda, pagina);

            var cantidadAbonos = factura.Pagos.Count;
            if (cantidadAbonos >= 2)
                return PagoInvalido("La factura alcanzó el límite máximo de dos cuotas permitidas", busqueda, pagina, facturaSeleccionada);
            if (request.Monto.Value > saldoPendiente)
                return PagoInvalido("El monto del pago no puede ser mayor al saldo pendiente", busqueda, pagina, facturaSeleccionada);

            var montoPago = decimal.Round(request.Monto.Value, 2, MidpointRounding.AwayFromZero);
            if (montoPago <= 0)
                return PagoInvalido("Ingrese un monto del pago válido", busqueda, pagina, facturaSeleccionada);
            if (montoPago > saldoPendiente)
                return PagoInvalido("El monto del pago no puede ser mayor al saldo pendiente", busqueda, pagina, facturaSeleccionada);
            if (cantidadAbonos == 1 && montoPago < saldoPendiente)
                return PagoInvalido(
                    "La factura ya cuenta con un primer abono; el segundo pago debe cubrir obligatoriamente la totalidad del saldo pendiente",
                    busqueda,
                    pagina,
                    facturaSeleccionada);

            factura.Pagos.Add(new PagoFactura
            {
                Monto = montoPago,
                FechaPago = request.FechaPago.Value.Date,
                MetodoPago = metodoPago,
                Referencia = string.IsNullOrWhiteSpace(request.Referencia) ? null : request.Referencia.Trim(),
                FechaRegistro = DateTime.UtcNow
            });
            var nuevoSaldo = saldoPendiente - montoPago;
            factura.EstadoPago = nuevoSaldo == 0
                ? "Pagado"
                : factura.FechaVencimiento.Date < DateTime.Today ? "Vencido" : "Pendiente";
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var estadoToast = EstadoVisible(factura.EstadoPago, factura.FechaVencimiento, DateTime.Today);
            TempData["ToastSuccess"] = $"Pago registrado. Saldo pendiente de {factura.NumeroFactura}: {nuevoSaldo.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))}. Estado: {estadoToast}";
            return RedirectToAction(nameof(RegistroPago), new { busqueda, pagina });
    }

    private IActionResult PagoInvalido(string mensaje, string? busqueda, int pagina, int? facturaId = null)
    {
        TempData["ToastError"] = mensaje;
        return RedirectToAction(nameof(RegistroPago), new { busqueda, pagina, facturaId });
    }

    private static bool BusquedaPagoValida(string termino) =>
        Regex.IsMatch(termino, @"^[\p{L}\p{N}\s\-\.&,/'()]+$");

    private IActionResult PdfNoDisponible(
        int? clienteId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        int pagina,
        string? origen = null,
        int? clienteCuentasId = null,
        string? estadoCuentas = null,
        DateTime? emisionDesde = null,
        DateTime? emisionHasta = null,
        DateTime? vencimientoDesde = null,
        DateTime? vencimientoHasta = null)
    {
        TempData["ToastError"] = "El documento electrónico no está disponible";
        if (origen == nameof(CuentasPorCobrar))
            return RedirectToAction(nameof(CuentasPorCobrar), new
            {
                clienteId = clienteCuentasId,
                estado = estadoCuentas,
                fechaEmisionDesde = emisionDesde,
                fechaEmisionHasta = emisionHasta,
                fechaVencimientoDesde = vencimientoDesde,
                fechaVencimientoHasta = vencimientoHasta,
                pagina
            });
        return RedirectToAction(nameof(FacturasPorVencer), new { clienteId, fechaDesde, fechaHasta, estado, pagina });
    }

    private static bool FechasInvalidas(DateTime? desde, DateTime? hasta) =>
        desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date;

    private static bool EsPagada(string estado) =>
        string.Equals(estado, "Pagado", StringComparison.OrdinalIgnoreCase)
        || string.Equals(estado, "Pagada", StringComparison.OrdinalIgnoreCase);

    private static decimal CalcularSaldoPendiente(Factura factura) =>
        EsPagada(factura.EstadoPago)
            ? 0
            : Math.Max(0, factura.Total - factura.Pagos.Sum(pago => pago.Monto));

    private static string EstadoCuenta(Factura factura, DateTime hoy)
    {
        if (EsPagada(factura.EstadoPago) || CalcularSaldoPendiente(factura) <= 0)
            return "Pagada";
        return factura.FechaVencimiento.Date < hoy.Date ? "Vencida" : "Al día";
    }

    private static int DiasHasta(DateTime vencimiento, DateTime hoy) => (vencimiento.Date - hoy.Date).Days;

    private static string EstadoVisible(string estado, DateTime vencimiento, DateTime hoy)
    {
        if (EsPagada(estado))
            return "Pagada";
        return vencimiento.Date < hoy.Date ? "Vencida" : "Por vencer";
    }
}
