using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Data.Validation;
using Prosegin.Web.Services.Nubefact;
using Prosegin.Web.Services.Nubefact.DTOs;
using Prosegin.Web.ViewModels.Pedidos;

namespace Prosegin.Web.Services;

public class PedidosService : IPedidosService
{
    private const int TamanoPagina = 5;
    private static readonly TimeZoneInfo ZonaHorariaEntrega = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
    private readonly ProseginDbContext _context;
    private readonly INubefactService? _nubefactService;
    private readonly NubefactSettings _nubefactSettings;

    public PedidosService(
        ProseginDbContext context,
        INubefactService? nubefactService = null,
        IOptions<NubefactSettings>? nubefactOptions = null)
    {
        _context = context;
        _nubefactService = nubefactService;
        _nubefactSettings = nubefactOptions?.Value ?? new NubefactSettings();
    }

    public async Task<PedidoListViewModel> ObtenerPedidosConfirmadosAsync(
        string? termino,
        string? estado,
        int pagina = 1,
        CancellationToken cancellationToken = default)
    {
        var pedidos = await ObtenerListaBasePedidosAsync(cancellationToken);
        pedidos = pedidos
            .Where(p => PedidoLogisticaRules.EsPedidoValidoParaLogistica(p.VentaAprobada, p.NumeroOrdenCompra))
            .ToList();

        var totalGeneral = pedidos.Count;
        var totalPorPreparar = pedidos.Count(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoPorPreparar);
        var totalEnPreparacion = pedidos.Count(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoEnPreparacion);
        var totalListoDespacho = pedidos.Count(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoListoDespacho);
        var totalEntregado = pedidos.Count(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoEntregado);
        var totalUrgentes = pedidos.Count(p => p.EsUrgenteMenor24h);

        var estadoNormalizado = string.IsNullOrWhiteSpace(estado)
            ? PedidoLogisticaRules.EstadoTodos
            : estado.Trim().ToUpperInvariant();
        if (estadoNormalizado != PedidoLogisticaRules.EstadoTodos)
        {
            pedidos = pedidos.Where(p => p.EstadoOperativo == estadoNormalizado).ToList();
        }

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var filtro = termino.Trim();
            pedidos = pedidos.Where(p => PedidoLogisticaRules.CoincideBusqueda(
                p.NumeroPedido,
                p.NumeroOrdenCompra,
                p.ClienteRuc,
                p.ClienteRazonSocial,
                filtro)).ToList();
        }

        pedidos = pedidos
            .OrderBy(p => p.FechaEntrega)
            .ThenBy(p => p.FechaConfirmacion)
            .ToList();

        var totalFiltrados = pedidos.Count;
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(totalFiltrados / (double)TamanoPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        return new PedidoListViewModel
        {
            Termino = termino?.Trim(),
            EstadoFiltro = estadoNormalizado,
            Pedidos = pedidos.Skip((pagina - 1) * TamanoPagina).Take(TamanoPagina).ToList(),
            Pagina = pagina,
            TamanoPagina = TamanoPagina,
            TotalFiltrados = totalFiltrados,
            TotalPaginas = totalPaginas,
            TotalPedidos = totalGeneral,
            TotalPorPreparar = totalPorPreparar,
            TotalEnPreparacion = totalEnPreparacion,
            TotalListoDespacho = totalListoDespacho,
            TotalEntregado = totalEntregado,
            TotalUrgentes = totalUrgentes,
            MensajeSinResultados = PedidoLogisticaRules.MensajeSinResultados
        };
    }

    public async Task<PedidoDetalleViewModel?> ObtenerDetallePedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        var pedidos = await ObtenerListaBasePedidosAsync(cancellationToken);
        return pedidos.FirstOrDefault(p =>
            string.Equals(p.NumeroPedido, numeroPedido, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<PedidoDetalleViewModel>> ObtenerListaBasePedidosAsync(CancellationToken cancellationToken)
    {
        var ordenesVenta = await _context.OrdenesVenta
            .AsNoTracking()
            .Include(ov => ov.Cotizacion)
                .ThenInclude(c => c.Cliente)
            .Include(ov => ov.Cotizacion)
                .ThenInclude(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
            .Include(ov => ov.OrdenesCompra)
            .Include(ov => ov.Facturas)
            .Include(ov => ov.GuiasRemision)
            .Where(ov => ov.Cotizacion.Estado == "Aprobada"
                && ov.Cotizacion.NumeroOrdenCompraCliente != null
                && ov.Cotizacion.NumeroOrdenCompraCliente != "")
            .ToListAsync(cancellationToken);

        var culture = CultureInfo.GetCultureInfo("es-PE");

        return ordenesVenta.Select(orden =>
        {
            var cotizacion = orden.Cotizacion;
            var fechaConfirmacion = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(orden.FechaCreacion, DateTimeKind.Utc),
                ZonaHorariaEntrega);
            var estado = NormalizarEstadoLogistico(orden.EstadoLogistico);
            return new PedidoDetalleViewModel
            {
                Id = orden.Id,
                NumeroPedido = cotizacion.Correlativo,
                NumeroOrdenCompra = cotizacion.NumeroOrdenCompraCliente!,
                ClienteRazonSocial = cotizacion.Cliente.RazonSocial,
                ClienteRuc = cotizacion.Cliente.Ruc,
                FechaConfirmacion = fechaConfirmacion,
                FechaEntrega = DateTime.MaxValue,
                FechaConfirmacionTexto = fechaConfirmacion.ToString("dd MMM yyyy HH:mm 'hrs'", culture),
                FechaEntregaTexto = "Por programar",
                EsUrgenteMenor24h = false,
                DireccionEntrega = cotizacion.Cliente.DireccionFiscal,
                SedeAlias = "Dirección fiscal del cliente",
                EstadoOperativo = estado,
                EstadoOperativoTexto = TextoEstado(estado),
                AlmacenOrigen = "Central Huachipa",
                Embalaje = "Por definir",
                VentaAprobada = true,
                PuedeGenerarOrdenesCompra = estado == PedidoLogisticaRules.EstadoPorPreparar
                    && orden.OrdenesCompra.Count == 0,
                PuedeGenerarFactura = estado == PedidoLogisticaRules.EstadoEntregado
                    && orden.Facturas.Count == 0
                    && EsClienteValidoParaFactura(cotizacion.Cliente),
                PuedeGenerarGuiaRemision = estado == PedidoLogisticaRules.EstadoListoDespacho
                    && !TieneGuiaRemisionActiva(orden.GuiasRemision),
                GuiaRemisionEmitida = TieneGuiaRemisionActiva(orden.GuiasRemision),
                NumeroGuiaRemision = orden.GuiasRemision.OrderByDescending(g => g.Id).Select(g => g.NumeroGuia).FirstOrDefault(),
                EnlacePdfGuia = orden.GuiasRemision.OrderByDescending(g => g.Id).Select(g => g.EnlacePdf).FirstOrDefault(),
                FacturaEmitida = orden.Facturas.Count > 0,
                NumeroFactura = orden.Facturas.OrderByDescending(f => f.Id).Select(f => f.NumeroFactura).FirstOrDefault(),
                EstadoEmisionFactura = orden.Facturas.OrderByDescending(f => f.Id).Select(f => f.EstadoEmision).FirstOrDefault(),
                EnlacePdfFactura = orden.Facturas.OrderByDescending(f => f.Id).Select(f => f.EnlacePdf).FirstOrDefault(),
                Subtotal = cotizacion.Subtotal,
                Igv = cotizacion.Igv,
                Total = cotizacion.Total,
                CondicionPago = cotizacion.CondicionPago,
                DireccionFiscal = cotizacion.Cliente.DireccionFiscal,
                Items = cotizacion.Detalles
                    .OrderBy(d => d.Id)
                    .Select(d => new PedidoItemViewModel
                    {
                        ProductoId = d.ProductoId,
                        Descripcion = d.Producto.Nombre,
                        Cantidad = d.Cantidad,
                        UnidadMedida = d.Producto.UnidadMedida,
                        Codigo = d.Producto.Sku,
                        PrecioUnitario = d.PrecioVentaCalculado,
                        Subtotal = d.Subtotal,
                        Igv = Redondear(d.Subtotal * 0.18m),
                        Total = Redondear(d.Subtotal * 1.18m)
                    })
                    .ToList()
            };
        }).ToList();
    }

    private static string NormalizarEstadoLogistico(string? estado)
    {
        return estado?.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant() switch
        {
            "PORPREPARAR" => PedidoLogisticaRules.EstadoPorPreparar,
            "ENPREPARACION" => PedidoLogisticaRules.EstadoEnPreparacion,
            "ENCONSOLIDACION" => PedidoLogisticaRules.EstadoEnPreparacion,
            "LISTOPARADESPACHO" => PedidoLogisticaRules.EstadoListoDespacho,
            "DESPACHADO" => PedidoLogisticaRules.EstadoListoDespacho,
            "ENTREGADO" => PedidoLogisticaRules.EstadoEntregado,
            _ => PedidoLogisticaRules.EstadoPorPreparar
        };
    }

    private static string TextoEstado(string estado)
    {
        return estado switch
        {
            PedidoLogisticaRules.EstadoPorPreparar => PedidoLogisticaRules.TextoPorPreparar,
            PedidoLogisticaRules.EstadoEnPreparacion => PedidoLogisticaRules.TextoEnPreparacion,
            PedidoLogisticaRules.EstadoEntregado => PedidoLogisticaRules.TextoEntregado,
            _ => PedidoLogisticaRules.TextoListoDespacho
        };
    }

    public async Task<FacturaPreviewViewModel?> PrevisualizarFacturaAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        var orden = await ObtenerOrdenParaFacturaAsync(numeroPedido, cancellationToken);
        var facturaExistente = orden?.Facturas
            .OrderByDescending(f => f.Id)
            .FirstOrDefault();
        if (orden == null || !EsClienteValidoParaFactura(orden.Cotizacion.Cliente)
            || !EsEstadoEntregado(orden.EstadoLogistico)
            || (facturaExistente != null && facturaExistente.EstadoEmision != NubefactService.EstadoRechazado))
        {
            return null;
        }

        var fechaEmision = DateTime.Now;
        var serie = ObtenerSerieFactura();
        var numero = (facturaExistente != null && facturaExistente.EstadoEmision == NubefactService.EstadoAceptado)
            ? facturaExistente.Numero
            : await ObtenerSiguienteNumeroAsync(serie, cancellationToken);
        return CrearPrevisualizacion(orden, fechaEmision, serie, numero);
    }

    public async Task<EmitirFacturaResult> EmitirFacturaAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        if (_nubefactService == null)
            return EmitirFacturaResult.Fallido("La integración Nubefact no está configurada.");

        var orden = await ObtenerOrdenParaFacturaAsync(numeroPedido, cancellationToken);

        if (orden == null)
            return EmitirFacturaResult.Fallido("Pedido no encontrado.");
        if (!EsEstadoEntregado(orden.EstadoLogistico))
            return EmitirFacturaResult.Fallido("Solo se puede facturar un pedido entregado.");
        if (!EsClienteValidoParaFactura(orden.Cotizacion.Cliente))
            return EmitirFacturaResult.Fallido("El cliente debe estar activo y habido en SUNAT.");
        if (!RucValidator.ValidarFormato(orden.Cotizacion.Cliente.Ruc, out var errorRuc))
            return EmitirFacturaResult.Fallido($"No se puede emitir la factura: {errorRuc}. Actualiza el RUC del cliente antes de reintentar.");

        var facturaExistente = orden.Facturas
            .OrderByDescending(f => f.Id)
            .FirstOrDefault();
        if (facturaExistente != null && facturaExistente.EstadoEmision != NubefactService.EstadoRechazado)
            return EmitirFacturaResult.Fallido("Este pedido ya tiene una factura registrada.");

        var fechaEmision = DateTime.Now;
        var serie = ObtenerSerieFactura();
        var numero = (facturaExistente != null && facturaExistente.EstadoEmision == NubefactService.EstadoAceptado)
            ? facturaExistente.Numero
            : await ObtenerSiguienteNumeroAsync(serie, cancellationToken);
        var preview = CrearPrevisualizacion(orden, fechaEmision, serie, numero);
        var factura = facturaExistente ?? new Factura
        {
            OrdenVentaId = orden.Id,
            ClienteId = orden.Cotizacion.ClienteId,
            NumeroFactura = $"{preview.Serie}-{preview.Numero}",
            Serie = preview.Serie,
            Numero = preview.Numero,
            FechaEmision = fechaEmision.ToUniversalTime(),
            FechaVencimiento = preview.FechaVencimiento.ToUniversalTime(),
            EstadoEmision = NubefactService.EstadoPendienteEnvio,
            EstadoPago = "Pendiente",
            Total = preview.Total,
            Detalles = preview.Items.Select(item => new FacturaDetalle
            {
                Codigo = item.Codigo,
                Descripcion = item.Descripcion,
                UnidadMedida = item.UnidadMedida,
                Cantidad = item.Cantidad,
                ValorUnitario = item.PrecioUnitario,
                PrecioUnitario = Redondear(item.PrecioUnitario * 1.18m),
                Descuento = 0m,
                Subtotal = item.Subtotal,
                Igv = item.Igv,
                Total = item.Total
            }).ToList()
        };

        if (facturaExistente != null)
        {
            factura.Serie = preview.Serie;
            factura.Numero = preview.Numero;
            factura.NumeroFactura = $"{preview.Serie}-{preview.Numero}";
            factura.FechaEmision = fechaEmision.ToUniversalTime();
            factura.FechaVencimiento = preview.FechaVencimiento.ToUniversalTime();
            factura.EstadoEmision = NubefactService.EstadoPendienteEnvio;
            factura.MensajeError = null;
        }

        if (facturaExistente == null)
        {
            _context.Facturas.Add(factura);
        }
        await _context.SaveChangesAsync(cancellationToken);

        NubefactResponseDto response = null!;
        for (int intento = 1; intento <= 5; intento++)
        {
            response = await _nubefactService.EnviarFacturaAsync(new NubefactFacturaRequest
            {
                TipoDeComprobante = 1,
                Serie = serie,
                Numero = numero,
                ClienteTipoDeDocumento = "6",
                ClienteNumeroDeDocumento = preview.ClienteRuc,
                ClienteDenominacion = preview.ClienteRazonSocial,
                ClienteDireccion = preview.DireccionFiscal,
                FechaDeEmision = preview.FechaEmision.ToString("dd-MM-yyyy"),
                FechaDeVencimiento = preview.FechaVencimiento.ToString("dd-MM-yyyy"),
                TotalGravada = preview.Subtotal,
                TotalIgv = preview.Igv,
                Total = preview.Total,
                OrdenCompraServicio = preview.NumeroOrdenCompra,
                CondicionesDePago = preview.CondicionPago,
                Items = preview.Items.Select(item => new NubefactFacturaItem
                {
                    Codigo = item.Codigo,
                    Descripcion = item.Descripcion,
                    UnidadDeMedida = MapearUnidadMedidaSunat(item.UnidadMedida),
                    Cantidad = item.Cantidad,
                    ValorUnitario = item.PrecioUnitario,
                    PrecioUnitario = Redondear(item.PrecioUnitario * 1.18m),
                    Subtotal = item.Subtotal,
                    Igv = item.Igv,
                    Total = item.Total
                }).ToList()
            }, cancellationToken);

            if (!response.Exito && (response.MensajeError?.Contains("ya existe", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                numero++;
                factura.Numero = numero;
                factura.NumeroFactura = $"{serie}-{numero}";
                continue;
            }

            break;
        }

        factura.Numero = numero;
        factura.NumeroFactura = $"{serie}-{numero}";
        factura.EstadoEmision = response.EstadoProceso;
        factura.CodigoHash = response.CodigoHash;
        factura.CadenaQr = response.CadenaQr;
        factura.EnlacePdf = response.EnlacePdf;
        factura.EnlaceXml = response.EnlaceXml;
        factura.EnlaceCdr = response.EnlaceCdr;
        factura.MensajeError = response.MensajeError ?? response.SunatDescription;
        factura.NumeroFactura = response.Serie is not null && response.Numero.HasValue
            ? $"{response.Serie}-{response.Numero}"
            : factura.NumeroFactura;
        await _context.SaveChangesAsync(cancellationToken);

        return response.Exito
            ? EmitirFacturaResult.Exitoso(factura.NumeroFactura, response)
            : EmitirFacturaResult.Pendiente(response.MensajeError ?? "La factura quedó pendiente de envío.", response);
    }

    public async Task<GuiaRemisionPreviewViewModel?> PrevisualizarGuiaRemisionAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        var orden = await ObtenerOrdenParaGuiaAsync(numeroPedido, cancellationToken);
        var guiaExistente = orden?.GuiasRemision
            .OrderByDescending(g => g.Id)
            .FirstOrDefault();
        if (orden == null || !EsEstadoListoParaDespacho(orden.EstadoLogistico)
            || (guiaExistente != null && guiaExistente.EstadoEmision != NubefactService.EstadoRechazado)
            || !TienePesosRegistrados(orden.Cotizacion.Detalles))
        {
            return null;
        }

        var serie = ObtenerSerieGuia();
        var numero = (guiaExistente != null && guiaExistente.EstadoEmision == NubefactService.EstadoAceptado)
            ? guiaExistente.Numero
            : await ObtenerSiguienteNumeroGuiaAsync(serie, cancellationToken);
        return CrearPrevisualizacionGuia(orden, DateTime.Now, serie, numero);
    }

    public async Task<EmitirGuiaRemisionResult> EmitirGuiaRemisionAsync(
        EmitirGuiaRemisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_nubefactService == null)
            return EmitirGuiaRemisionResult.Fallido("La integración Nubefact no está configurada.");
        if (string.IsNullOrWhiteSpace(request.ConductorNombre))
            return EmitirGuiaRemisionResult.Fallido("Ingrese el nombre del conductor.");
        if (string.IsNullOrWhiteSpace(request.PlacaVehiculo))
            return EmitirGuiaRemisionResult.Fallido("Ingrese la placa del vehículo.");

        var orden = await ObtenerOrdenParaGuiaAsync(request.NumeroPedido, cancellationToken);
        if (orden == null)
            return EmitirGuiaRemisionResult.Fallido("Pedido no encontrado.");
        if (!EsEstadoListoParaDespacho(orden.EstadoLogistico))
            return EmitirGuiaRemisionResult.Fallido("Solo se puede emitir una guía para un pedido listo para despacho.");
        var guiaExistente = orden.GuiasRemision
            .OrderByDescending(g => g.Id)
            .FirstOrDefault();
        if (guiaExistente != null && guiaExistente.EstadoEmision != NubefactService.EstadoRechazado)
            return EmitirGuiaRemisionResult.Fallido("Este pedido ya tiene una guía de remisión registrada.");
        if (!TienePesosRegistrados(orden.Cotizacion.Detalles))
            return EmitirGuiaRemisionResult.Fallido("Registre el peso unitario de todos los productos antes de emitir la guía.");

        var serie = ObtenerSerieGuia();
        var numero = (guiaExistente != null && guiaExistente.EstadoEmision == NubefactService.EstadoAceptado)
            ? guiaExistente.Numero
            : await ObtenerSiguienteNumeroGuiaAsync(serie, cancellationToken);
        var fecha = DateTime.Now;
        var preview = CrearPrevisualizacionGuia(orden, fecha, serie, numero);
        var guia = guiaExistente ?? new GuiaRemision
        {
            OrdenVentaId = orden.Id,
            ClienteId = orden.Cotizacion.ClienteId,
            NumeroGuia = $"{serie}-{numero}",
            Serie = serie,
            Numero = numero,
            FechaEmision = fecha.ToUniversalTime(),
            FechaInicioTraslado = fecha.ToUniversalTime(),
            PesoBrutoTotal = preview.PesoBrutoTotal,
            NumeroBultos = preview.NumeroBultos,
            PuntoPartidaDireccion = preview.PuntoPartidaDireccion,
            PuntoPartidaUbigeo = preview.PuntoPartidaUbigeo,
            PuntoLlegadaDireccion = preview.PuntoLlegadaDireccion,
            PuntoLlegadaUbigeo = preview.PuntoLlegadaUbigeo,
            ConductorNombre = request.ConductorNombre.Trim(),
            ConductorDocumento = request.ConductorDocumento?.Trim(),
            ConductorLicencia = request.ConductorLicencia?.Trim(),
            PlacaVehiculo = request.PlacaVehiculo.Trim().ToUpperInvariant(),
            Detalles = preview.Items.Select(item => new GuiaRemisionDetalle
            {
                ProductoId = item.ProductoId,
                Codigo = item.Codigo,
                Descripcion = item.Descripcion,
                UnidadMedida = item.UnidadMedida,
                Cantidad = item.Cantidad,
                PesoUnitarioKg = item.PesoUnitarioKg,
                PesoTotalKg = item.PesoTotalKg
            }).ToList()
        };

        if (guiaExistente != null)
        {
            guia.Serie = serie;
            guia.Numero = numero;
            guia.NumeroGuia = $"{serie}-{numero}";
            guia.FechaEmision = fecha.ToUniversalTime();
            guia.FechaInicioTraslado = fecha.ToUniversalTime();
            guia.ConductorNombre = request.ConductorNombre.Trim();
            guia.ConductorDocumento = request.ConductorDocumento?.Trim();
            guia.ConductorLicencia = request.ConductorLicencia?.Trim();
            guia.PlacaVehiculo = request.PlacaVehiculo.Trim().ToUpperInvariant();
            guia.MensajeError = null;
            guia.EstadoEmision = NubefactService.EstadoPendienteEnvio;
        }

        if (guiaExistente == null)
        {
            _context.GuiasRemision.Add(guia);
        }
        await _context.SaveChangesAsync(cancellationToken);

        NubefactResponseDto response = null!;
        for (int intento = 1; intento <= 5; intento++)
        {
            response = await _nubefactService.EnviarGuiaRemisionAsync(new NubefactGuiaRequest
            {
                Serie = serie,
                Numero = numero,
                MotivoDeTraslado = "01",
                TipoDeTransporte = "02",
                ClienteNumeroDeDocumento = preview.ClienteRuc,
                ClienteDenominacion = preview.ClienteRazonSocial,
                ClienteDireccion = preview.PuntoLlegadaDireccion,
                FechaDeEmision = preview.FechaEmision.ToString("dd-MM-yyyy"),
                FechaDeInicioDeTraslado = preview.FechaInicioTraslado.ToString("dd-MM-yyyy"),
                PesoBrutoTotal = preview.PesoBrutoTotal,
                NumeroDeBultos = preview.NumeroBultos,
                PuntoDePartidaDireccion = preview.PuntoPartidaDireccion,
                PuntoDePartidaUbigeo = preview.PuntoPartidaUbigeo ?? string.Empty,
                PuntoDeLlegadaDireccion = preview.PuntoLlegadaDireccion,
                PuntoDeLlegadaUbigeo = preview.PuntoLlegadaUbigeo ?? string.Empty,
                ConductorDenominacion = guia.ConductorNombre,
                ConductorDocumentoTipo = string.IsNullOrWhiteSpace(guia.ConductorDocumento) ? null : "1",
                ConductorDocumentoNumero = guia.ConductorDocumento,
                ConductorNumeroLicencia = guia.ConductorLicencia,
                TransportistaPlacaNumero = guia.PlacaVehiculo,
                Items = preview.Items.Select(item => new NubefactGuiaItem
                {
                    Codigo = item.Codigo,
                    Descripcion = item.Descripcion,
                    UnidadDeMedida = MapearUnidadMedidaSunat(item.UnidadMedida),
                    Cantidad = item.Cantidad
                }).ToList()
            }, cancellationToken);

            if (!response.Exito && (response.MensajeError?.Contains("ya existe", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                numero++;
                guia.Numero = numero;
                guia.NumeroGuia = $"{serie}-{numero}";
                continue;
            }

            break;
        }

        guia.Numero = numero;
        guia.NumeroGuia = $"{serie}-{numero}";
        guia.EstadoEmision = response.EstadoProceso;
        guia.CodigoHash = response.CodigoHash;
        guia.CadenaQr = response.CadenaQr;
        guia.EnlacePdf = response.EnlacePdf;
        guia.EnlaceXml = response.EnlaceXml;
        guia.EnlaceCdr = response.EnlaceCdr;
        guia.MensajeError = response.MensajeError ?? response.SunatDescription;
        if (response.Serie is not null && response.Numero.HasValue)
        {
            guia.Serie = response.Serie;
            guia.Numero = response.Numero.Value;
            guia.NumeroGuia = $"{response.Serie}-{response.Numero.Value}";
        }

        if (response.Exito)
        {
            orden.EstadoLogistico = "Entregado";
        }

        await _context.SaveChangesAsync(cancellationToken);

        return response.Exito
            ? EmitirGuiaRemisionResult.Exitoso(guia.NumeroGuia, response)
            : EmitirGuiaRemisionResult.Pendiente(response.MensajeError ?? "La guía quedó pendiente de envío.", response);
    }

    private async Task<OrdenVenta?> ObtenerOrdenParaFacturaAsync(
        string numeroPedido,
        CancellationToken cancellationToken)
    {
        return await _context.OrdenesVenta
            .Include(ov => ov.Cotizacion)
                .ThenInclude(c => c.Cliente)
            .Include(ov => ov.Cotizacion)
                .ThenInclude(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
            .Include(ov => ov.Facturas)
            .Include(ov => ov.GuiasRemision)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                ov => ov.Cotizacion.Correlativo == numeroPedido,
                cancellationToken);
    }

    private static FacturaPreviewViewModel CrearPrevisualizacion(
        OrdenVenta orden,
        DateTime fechaEmision,
        string serie,
        int numero)
    {
        var cotizacion = orden.Cotizacion;
        var plazo = ExtraerDias(cotizacion.CondicionPago);
        return new FacturaPreviewViewModel
        {
            OrdenVentaId = orden.Id,
            NumeroPedido = cotizacion.Correlativo,
            NumeroOrdenCompra = cotizacion.NumeroOrdenCompraCliente ?? string.Empty,
            ClienteRazonSocial = cotizacion.Cliente.RazonSocial,
            ClienteRuc = cotizacion.Cliente.Ruc,
            DireccionFiscal = cotizacion.Cliente.DireccionFiscal,
            CondicionPago = cotizacion.CondicionPago,
            FechaEmision = fechaEmision,
            FechaVencimiento = fechaEmision.AddDays(plazo),
            Serie = serie,
            Numero = numero,
            Subtotal = cotizacion.Subtotal,
            Igv = cotizacion.Igv,
            Total = cotizacion.Total,
            Items = cotizacion.Detalles.OrderBy(d => d.Id).Select(d => new PedidoItemViewModel
            {
                ProductoId = d.ProductoId,
                Codigo = d.Producto.Sku,
                Descripcion = string.IsNullOrWhiteSpace(d.NombreProducto) ? d.Producto.Nombre : d.NombreProducto,
                Cantidad = d.Cantidad,
                UnidadMedida = d.Producto.UnidadMedida,
                PrecioUnitario = d.PrecioVentaCalculado,
                Subtotal = d.Subtotal,
                Igv = Redondear(d.Subtotal * 0.18m),
                Total = Redondear(d.Subtotal * 1.18m)
            }).ToList()
        };
    }

    private string ObtenerSerieFactura() =>
        string.IsNullOrWhiteSpace(_nubefactSettings.SerieFactura)
            ? "FFF1"
            : _nubefactSettings.SerieFactura.Trim();

    private string ObtenerSerieGuia() =>
        string.IsNullOrWhiteSpace(_nubefactSettings.SerieGuia) ? "TTT1" : _nubefactSettings.SerieGuia.Trim();

    private async Task<int> ObtenerSiguienteNumeroAsync(string serie, CancellationToken cancellationToken)
    {
        var ultimoNumero = await _context.Facturas
            .Where(f => f.Serie == serie)
            .Select(f => (int?)f.Numero)
            .MaxAsync(cancellationToken);
        var baseNumero = _nubefactSettings.CorrelativoInicialFactura > 0 ? _nubefactSettings.CorrelativoInicialFactura - 1 : 0;
        return Math.Max(ultimoNumero ?? 0, baseNumero) + 1;
    }

    private async Task<int> ObtenerSiguienteNumeroGuiaAsync(string serie, CancellationToken cancellationToken)
    {
        var ultimoNumero = await _context.GuiasRemision
            .Where(g => g.Serie == serie)
            .Select(g => (int?)g.Numero)
            .MaxAsync(cancellationToken);
        var baseNumero = _nubefactSettings.CorrelativoInicialGuia > 0 ? _nubefactSettings.CorrelativoInicialGuia - 1 : 0;
        return Math.Max(ultimoNumero ?? 0, baseNumero) + 1;
    }

    private async Task<OrdenVenta?> ObtenerOrdenParaGuiaAsync(string numeroPedido, CancellationToken cancellationToken)
    {
        return await _context.OrdenesVenta
            .Include(ov => ov.Cotizacion).ThenInclude(c => c.Cliente)
            .Include(ov => ov.Cotizacion).ThenInclude(c => c.Detalles).ThenInclude(d => d.Producto)
            .Include(ov => ov.GuiasRemision)
            .AsSplitQuery()
            .SingleOrDefaultAsync(ov => ov.Cotizacion.Correlativo == numeroPedido, cancellationToken);
    }

    private static GuiaRemisionPreviewViewModel CrearPrevisualizacionGuia(
        OrdenVenta orden, DateTime fecha, string serie, int numero)
    {
        var cotizacion = orden.Cotizacion;
        var items = cotizacion.Detalles.OrderBy(d => d.Id).Select(d => new GuiaRemisionItemViewModel
        {
            ProductoId = d.ProductoId,
            Codigo = d.Producto.Sku,
            Descripcion = string.IsNullOrWhiteSpace(d.NombreProducto) ? d.Producto.Nombre : d.NombreProducto,
            UnidadMedida = d.Producto.UnidadMedida,
            Cantidad = d.Cantidad,
            PesoUnitarioKg = d.Producto.PesoKg,
            PesoTotalKg = RedondearPeso(d.Producto.PesoKg * d.Cantidad)
        }).ToList();
        return new GuiaRemisionPreviewViewModel
        {
            OrdenVentaId = orden.Id,
            NumeroPedido = cotizacion.Correlativo,
            NumeroOrdenCompra = cotizacion.NumeroOrdenCompraCliente ?? string.Empty,
            ClienteRazonSocial = cotizacion.Cliente.RazonSocial,
            ClienteRuc = cotizacion.Cliente.Ruc,
            PuntoPartidaDireccion = "Central Huachipa",
            PuntoPartidaUbigeo = "150114",
            PuntoLlegadaDireccion = cotizacion.Cliente.DireccionFiscal,
            PuntoLlegadaUbigeo = cotizacion.Cliente.Ubigeo,
            FechaEmision = fecha,
            FechaInicioTraslado = fecha,
            Serie = serie,
            Numero = numero,
            PesoBrutoTotal = RedondearPeso(items.Sum(i => i.PesoTotalKg)),
            NumeroBultos = Math.Max(1, (int)Math.Ceiling(items.Sum(i => i.Cantidad))),
            Items = items
        };
    }

    private static bool TienePesosRegistrados(IEnumerable<CotizacionDetalle> detalles) =>
        detalles.Any() && detalles.All(d => d.Producto.PesoKg > 0);

    private static bool EsClienteValidoParaFactura(Cliente cliente) =>
        cliente.Activo
        && string.Equals(cliente.EstadoSunat?.Trim(), "ACTIVO", StringComparison.OrdinalIgnoreCase)
        && string.Equals(cliente.CondicionSunat?.Trim(), "HABIDO", StringComparison.OrdinalIgnoreCase);

    private static bool EsEstadoListoParaDespacho(string? estado) =>
        string.Equals(estado?.Replace(" ", string.Empty), "ListoParaDespacho", StringComparison.OrdinalIgnoreCase)
        || string.Equals(estado, "Despachado", StringComparison.OrdinalIgnoreCase);

    private static bool EsEstadoEntregado(string? estado) =>
        string.Equals(estado?.Replace(" ", string.Empty), "Entregado", StringComparison.OrdinalIgnoreCase);

    private static bool TieneGuiaRemisionActiva(IEnumerable<GuiaRemision> guias) =>
        guias.Any(g => g.EstadoEmision != NubefactService.EstadoRechazado);

    private static int ExtraerDias(string? condicionPago)
    {
        var numero = new string((condicionPago ?? string.Empty).Where(char.IsDigit).ToArray());
        return int.TryParse(numero, out var dias) && dias > 0 ? dias : 7;
    }

    public static string MapearUnidadMedidaSunat(string? unidadMedida)
    {
        if (string.IsNullOrWhiteSpace(unidadMedida))
            return "NIU";

        return unidadMedida.Trim().ToUpperInvariant() switch
        {
            "PAR" or "PARES" or "PR" => "PAR",
            "CJA" or "CAJA" or "CAJAS" or "BX" => "BX",
            "SET" or "JGO" or "JUEGO" or "JUEGOS" => "SET",
            "KG" or "KGM" or "KILOGRAMO" or "KILOGRAMOS" => "KGM",
            "M" or "MTR" or "METRO" or "METROS" => "MTR",
            "LTR" or "LT" or "LITRO" or "LITROS" => "LTR",
            "DZN" or "DOC" or "DOCENA" or "DOCENAS" => "DZN",
            "GLI" or "GL" or "GALON" or "GALONES" => "GLI",
            "UND" or "UNI" or "UNIDAD" or "UNIDADES" or "PZA" or "PIEZA" or "NIU" => "NIU",
            _ => unidadMedida.Trim().Length <= 3 ? unidadMedida.Trim().ToUpperInvariant() : "NIU"
        };
    }

    private static decimal Redondear(decimal importe) => Math.Round(importe, 2, MidpointRounding.AwayFromZero);
    private static decimal RedondearPeso(decimal importe) => Math.Round(importe, 3, MidpointRounding.AwayFromZero);
}

public sealed class EmitirFacturaResult
{
    public bool Exito { get; private init; }
    public bool QuedoPendiente { get; private init; }
    public string Mensaje { get; private init; } = string.Empty;
    public string? NumeroFactura { get; private init; }
    public NubefactResponseDto? Respuesta { get; private init; }

    public static EmitirFacturaResult Exitoso(string numero, NubefactResponseDto respuesta) =>
        new() { Exito = true, NumeroFactura = numero, Mensaje = "Factura emitida con éxito", Respuesta = respuesta };

    public static EmitirFacturaResult Pendiente(string mensaje, NubefactResponseDto respuesta) =>
        new() { QuedoPendiente = true, Mensaje = mensaje, Respuesta = respuesta };

    public static EmitirFacturaResult Fallido(string mensaje) =>
        new() { Mensaje = mensaje };
}

public sealed class EmitirGuiaRemisionResult
{
    public bool Exito { get; private init; }
    public bool QuedoPendiente { get; private init; }
    public string Mensaje { get; private init; } = string.Empty;
    public string? NumeroGuia { get; private init; }
    public NubefactResponseDto? Respuesta { get; private init; }

    public static EmitirGuiaRemisionResult Exitoso(string numero, NubefactResponseDto respuesta) =>
        new() { Exito = true, NumeroGuia = numero, Mensaje = "Guía de remisión emitida con éxito", Respuesta = respuesta };
    public static EmitirGuiaRemisionResult Pendiente(string mensaje, NubefactResponseDto respuesta) =>
        new() { QuedoPendiente = true, Mensaje = mensaje, Respuesta = respuesta };
    public static EmitirGuiaRemisionResult Fallido(string mensaje) => new() { Mensaje = mensaje };
}
