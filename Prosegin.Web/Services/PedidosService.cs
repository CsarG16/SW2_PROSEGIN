using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Data.Validation;
using Prosegin.Web.ViewModels.Pedidos;

namespace Prosegin.Web.Services;

public class PedidosService : IPedidosService
{
    private const int TamanoPagina = 5;
    private static readonly TimeZoneInfo ZonaHorariaEntrega = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
    private readonly ProseginDbContext _context;

    public PedidosService(ProseginDbContext context)
    {
        _context = context;
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
                Items = cotizacion.Detalles
                    .OrderBy(d => d.Id)
                    .Select(d => new PedidoItemViewModel
                    {
                        ProductoId = d.ProductoId,
                        Descripcion = d.Producto.Nombre,
                        Cantidad = d.Cantidad,
                        UnidadMedida = d.Producto.UnidadMedida
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
            "ENTREGADO" => PedidoLogisticaRules.EstadoListoDespacho,
            _ => PedidoLogisticaRules.EstadoPorPreparar
        };
    }

    private static string TextoEstado(string estado)
    {
        return estado switch
        {
            PedidoLogisticaRules.EstadoPorPreparar => PedidoLogisticaRules.TextoPorPreparar,
            PedidoLogisticaRules.EstadoEnPreparacion => PedidoLogisticaRules.TextoEnPreparacion,
            _ => PedidoLogisticaRules.TextoListoDespacho
        };
    }
}
