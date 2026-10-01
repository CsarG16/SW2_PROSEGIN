using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Validation;
using Prosegin.Web.ViewModels.Pedidos;

namespace Prosegin.Web.Services;

public class PedidosService : IPedidosService
{
    private readonly ProseginDbContext _context;

    public PedidosService(ProseginDbContext context)
    {
        _context = context;
    }

    public async Task<PedidoListViewModel> ObtenerPedidosConfirmadosAsync(
        string? termino,
        string? estado,
        CancellationToken cancellationToken = default)
    {
        // 1. Obtener lista base de órdenes confirmadas
        var pedidos = await ObtenerListaBasePedidosAsync(cancellationToken);

        // 2. Regla clave: Solo se listan pedidos con venta aprobada y su respectiva orden de compra asignada;
        // las cotizaciones en borrador o pendientes quedan excluidas.
        pedidos = pedidos
            .Where(p => PedidoLogisticaRules.EsPedidoValidoParaLogistica(p.VentaAprobada, p.NumeroOrdenCompra))
            .ToList();

        // 3. Contadores globales antes de filtrar por fase o búsqueda para las pestañas de carga de trabajo
        var totalGeneral = pedidos.Count;
        var totalPorPreparar = pedidos.Count(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoPorPreparar);
        var totalEnPreparacion = pedidos.Count(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoEnPreparacion);
        var totalListoDespacho = pedidos.Count(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoListoDespacho);
        var totalUrgentes = pedidos.Count(p => p.EsUrgenteMenor24h);

        // 4. Aplicar filtro por estado operativo (pestaña)
        var estadoNormalizado = string.IsNullOrWhiteSpace(estado) ? PedidoLogisticaRules.EstadoTodos : estado.Trim().ToUpperInvariant();
        if (estadoNormalizado != PedidoLogisticaRules.EstadoTodos)
        {
            pedidos = pedidos.Where(p => p.EstadoOperativo == estadoNormalizado).ToList();
        }

        // 5. Aplicar filtro por término de búsqueda (N° Pedido, OC, RUC o Razón Social)
        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            pedidos = pedidos.Where(p => PedidoLogisticaRules.CoincideBusqueda(
                p.NumeroPedido,
                p.NumeroOrdenCompra,
                p.ClienteRuc,
                p.ClienteRazonSocial,
                t
            )).ToList();
        }

        // 6. Orden cronológico obligatorio: Fecha de entrega más próxima arriba; si coinciden, primero el confirmado primero
        pedidos = pedidos
            .OrderBy(p => p.FechaEntrega)
            .ThenBy(p => p.FechaConfirmacion)
            .ToList();

        return new PedidoListViewModel
        {
            Termino = termino?.Trim(),
            EstadoFiltro = estadoNormalizado,
            Pedidos = pedidos,
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

    private Task<List<PedidoDetalleViewModel>> ObtenerListaBasePedidosAsync(CancellationToken cancellationToken)
    {
        var baseDate = DateTime.Now;

        var lista = new List<PedidoDetalleViewModel>
        {
            // 1. PED-2025-0842 (Urgente: HOY 16:00 hrs)
            new PedidoDetalleViewModel
            {
                Id = 1,
                NumeroPedido = "PED-2025-0842",
                NumeroOrdenCompra = "OC-77821",
                ClienteRazonSocial = "Consorcio Vial Andino S.R.C.",
                ClienteRuc = "20492817263",
                FechaConfirmacion = new DateTime(2025, 3, 26, 10, 14, 0),
                FechaEntrega = DateTime.Today.AddHours(16),
                FechaConfirmacionTexto = "26 Mar 2025 10:14 hrs",
                FechaEntregaTexto = "HOY 16:00 hrs",
                EsUrgenteMenor24h = true,
                DireccionEntrega = "Av. Industrial 450, Almacén 4 - Lurín, Lima",
                SedeAlias = "Almacén Logístico Lurín",
                EstadoOperativo = PedidoLogisticaRules.EstadoPorPreparar,
                EstadoOperativoTexto = PedidoLogisticaRules.TextoPorPreparar,
                AlmacenOrigen = "Central Huachipa",
                Embalaje = "Packs Paletizados",
                VentaAprobada = true,
                Items = new List<PedidoItemViewModel>
                {
                    new() { Descripcion = "Casco Dieléctrico Tipo II ANSI Z89.1 (Amarillo)", Cantidad = 120, UnidadMedida = "Und" },
                    new() { Descripcion = "Lentes de Seguridad Anti-empañamiento 3M", Cantidad = 250, UnidadMedida = "Und" },
                    new() { Descripcion = "Guantes de Nitrilo con soporte Kevlar Calibre 13", Cantidad = 180, UnidadMedida = "Pares" }
                }
            },

            // 2. PED-2025-0839 (Urgente: Mañana 09:30 hrs)
            new PedidoDetalleViewModel
            {
                Id = 2,
                NumeroPedido = "PED-2025-0839",
                NumeroOrdenCompra = "OC-77810",
                ClienteRazonSocial = "Constructora e Ingeniería Minera del Sur S.A.C.",
                ClienteRuc = "20601928471",
                FechaConfirmacion = new DateTime(2025, 3, 26, 8, 45, 0),
                FechaEntrega = DateTime.Today.AddDays(1).AddHours(9).AddMinutes(30),
                FechaConfirmacionTexto = "26 Mar 2025 08:45 hrs",
                FechaEntregaTexto = "Mañana 09:30 hrs",
                EsUrgenteMenor24h = true,
                DireccionEntrega = "Km 18.5 Carretera Variante Uchumayo, Arequipa",
                SedeAlias = "Planta Variante Uchumayo",
                EstadoOperativo = PedidoLogisticaRules.EstadoEnPreparacion,
                EstadoOperativoTexto = PedidoLogisticaRules.TextoEnPreparacion,
                AlmacenOrigen = "Central Huachipa",
                Embalaje = "Cajas Reforzadas",
                VentaAprobada = true,
                Items = new List<PedidoItemViewModel>
                {
                    new() { Descripcion = "Botas de Seguridad Industrial Bata Industrials Titan con puntera de acero", Cantidad = 80, UnidadMedida = "Pares" },
                    new() { Descripcion = "Respirador de Media Pieza Facial Serie 6000 3M 6200", Cantidad = 95, UnidadMedida = "Und" },
                    new() { Descripcion = "Tapones Auditivos de Espuma sin Cordón 3M 1100 NRR 29dB", Cantidad = 400, UnidadMedida = "Pares" }
                }
            },

            // 3. PED-2025-0831 (28 Mar 2025)
            new PedidoDetalleViewModel
            {
                Id = 3,
                NumeroPedido = "PED-2025-0831",
                NumeroOrdenCompra = "OC-77785",
                ClienteRazonSocial = "Minera del Norte S.A.C.",
                ClienteRuc = "20554189312",
                FechaConfirmacion = new DateTime(2025, 3, 25, 16, 30, 0),
                FechaEntrega = new DateTime(2025, 3, 28, 14, 0, 0),
                FechaConfirmacionTexto = "25 Mar 2025 16:30 hrs",
                FechaEntregaTexto = "28 Mar 2025",
                EsUrgenteMenor24h = false,
                DireccionEntrega = "Base Mina Sector 3, Huamachuco, La Libertad",
                SedeAlias = "Campamento Base Huamachuco",
                EstadoOperativo = PedidoLogisticaRules.EstadoEnPreparacion,
                EstadoOperativoTexto = PedidoLogisticaRules.TextoEnPreparacion,
                AlmacenOrigen = "Central Huachipa",
                Embalaje = "Huacales de Madera",
                VentaAprobada = true,
                Items = new List<PedidoItemViewModel>
                {
                    new() { Descripcion = "Línea de Vida Doble Terminal con Absorbedor de Impacto Hawk 1.8m", Cantidad = 30, UnidadMedida = "Und" },
                    new() { Descripcion = "Bloque Retráctil de Cable de Acero Galvanizado Hawk 6m", Cantidad = 15, UnidadMedida = "Und" },
                    new() { Descripcion = "Mosquetón de Acero con Triple Bloqueo Automático 50 KN", Cantidad = 70, UnidadMedida = "Und" }
                }
            },

            // 4. PED-2025-0824 (30 Mar 2025)
            new PedidoDetalleViewModel
            {
                Id = 4,
                NumeroPedido = "PED-2025-0824",
                NumeroOrdenCompra = "OC-77742",
                ClienteRazonSocial = "Constructora del Pacífico S.A.C.",
                ClienteRuc = "20112839401",
                FechaConfirmacion = new DateTime(2025, 3, 24, 11, 20, 0),
                FechaEntrega = new DateTime(2025, 3, 30, 11, 0, 0),
                FechaConfirmacionTexto = "24 Mar 2025 11:20 hrs",
                FechaEntregaTexto = "30 Mar 2025",
                EsUrgenteMenor24h = false,
                DireccionEntrega = "Av. Néstor Gambetta 920, Callao",
                SedeAlias = "Planta Industrial Callao",
                EstadoOperativo = PedidoLogisticaRules.EstadoListoDespacho,
                EstadoOperativoTexto = PedidoLogisticaRules.TextoListoDespacho,
                AlmacenOrigen = "Central Huachipa",
                Embalaje = "Packs Paletizados",
                VentaAprobada = true,
                Items = new List<PedidoItemViewModel>
                {
                    new() { Descripcion = "Casco de Seguridad Industrial 3M H-700 con suspensión Ratchet", Cantidad = 60, UnidadMedida = "Und" },
                    new() { Descripcion = "Lentes de Seguridad 3M Virtua Plus Antirrayadura", Cantidad = 120, UnidadMedida = "Und" },
                    new() { Descripcion = "Mandil de Cuero Cromo para Trabajos en Caliente y Soldadura", Cantidad = 40, UnidadMedida = "Und" }
                }
            },

            // 5. PED-2025-0845 (31 Mar 2025)
            new PedidoDetalleViewModel
            {
                Id = 5,
                NumeroPedido = "PED-2025-0845",
                NumeroOrdenCompra = "OC-77830",
                ClienteRazonSocial = "Constructora e Ingeniería Minera del Sur S.A.C.",
                ClienteRuc = "20601928471",
                FechaConfirmacion = new DateTime(2025, 3, 26, 14, 10, 0),
                FechaEntrega = new DateTime(2025, 3, 31, 10, 0, 0),
                FechaConfirmacionTexto = "26 Mar 2025 14:10 hrs",
                FechaEntregaTexto = "31 Mar 2025",
                EsUrgenteMenor24h = false,
                DireccionEntrega = "Carretera Panamericana Sur Km 450, Marcona",
                SedeAlias = "Campamento Minero Sur",
                EstadoOperativo = PedidoLogisticaRules.EstadoPorPreparar,
                EstadoOperativoTexto = PedidoLogisticaRules.TextoPorPreparar,
                AlmacenOrigen = "Central Huachipa",
                Embalaje = "Packs Paletizados",
                VentaAprobada = true,
                Items = new List<PedidoItemViewModel>
                {
                    new() { Descripcion = "Guante de Nitrilo para Protección Química Ansell Solvex 37-175", Cantidad = 110, UnidadMedida = "Pares" },
                    new() { Descripcion = "Respirador de Media Pieza Facial Serie 6000 3M 6200", Cantidad = 50, UnidadMedida = "Und" }
                }
            },

            // 6. PED-2025-0835 (02 Abr 2025)
            new PedidoDetalleViewModel
            {
                Id = 6,
                NumeroPedido = "PED-2025-0835",
                NumeroOrdenCompra = "OC-77790",
                ClienteRazonSocial = "Minera del Norte S.A.C.",
                ClienteRuc = "20554189312",
                FechaConfirmacion = new DateTime(2025, 3, 25, 18, 0, 0),
                FechaEntrega = new DateTime(2025, 4, 2, 12, 0, 0),
                FechaConfirmacionTexto = "25 Mar 2025 18:00 hrs",
                FechaEntregaTexto = "02 Abr 2025",
                EsUrgenteMenor24h = false,
                DireccionEntrega = "Base Mina Sector 3, Huamachuco, La Libertad",
                SedeAlias = "Base Mina Sector 3",
                EstadoOperativo = PedidoLogisticaRules.EstadoEnPreparacion,
                EstadoOperativoTexto = PedidoLogisticaRules.TextoEnPreparacion,
                AlmacenOrigen = "Central Huachipa",
                Embalaje = "Cajas Selladas",
                VentaAprobada = true,
                Items = new List<PedidoItemViewModel>
                {
                    new() { Descripcion = "Goggle de Seguridad Panorámico 3M Goggle Gear Serie 500", Cantidad = 45, UnidadMedida = "Und" },
                    new() { Descripcion = "Casco Dieléctrico Tipo II ANSI Z89.1", Cantidad = 80, UnidadMedida = "Und" }
                }
            },

            // 7. PED-2025-0820 (03 Abr 2025)
            new PedidoDetalleViewModel
            {
                Id = 7,
                NumeroPedido = "PED-2025-0820",
                NumeroOrdenCompra = "OC-77730",
                ClienteRazonSocial = "Constructora del Pacífico S.A.C.",
                ClienteRuc = "20112839401",
                FechaConfirmacion = new DateTime(2025, 3, 23, 15, 30, 0),
                FechaEntrega = new DateTime(2025, 4, 3, 16, 0, 0),
                FechaConfirmacionTexto = "23 Mar 2025 15:30 hrs",
                FechaEntregaTexto = "03 Abr 2025",
                EsUrgenteMenor24h = false,
                DireccionEntrega = "Av. Industrial 1450, Ate - Lima",
                SedeAlias = "Sede Principal Ate",
                EstadoOperativo = PedidoLogisticaRules.EstadoListoDespacho,
                EstadoOperativoTexto = PedidoLogisticaRules.TextoListoDespacho,
                AlmacenOrigen = "Central Huachipa",
                Embalaje = "Packs Paletizados",
                VentaAprobada = true,
                Items = new List<PedidoItemViewModel>
                {
                    new() { Descripcion = "Guantes de Nitrilo Multipropósito Showa 370", Cantidad = 120, UnidadMedida = "Pares" },
                    new() { Descripcion = "Tapones Auditivos de Espuma sin Cordón 3M 1100", Cantidad = 300, UnidadMedida = "Pares" }
                }
            },

            // 8. PED-2025-0848 (05 Abr 2025)
            new PedidoDetalleViewModel
            {
                Id = 8,
                NumeroPedido = "PED-2025-0848",
                NumeroOrdenCompra = "OC-77850",
                ClienteRazonSocial = "Consorcio Vial Andino S.R.C.",
                ClienteRuc = "20492817263",
                FechaConfirmacion = new DateTime(2025, 3, 27, 9, 0, 0),
                FechaEntrega = new DateTime(2025, 4, 5, 11, 0, 0),
                FechaConfirmacionTexto = "27 Mar 2025 09:00 hrs",
                FechaEntregaTexto = "05 Abr 2025",
                EsUrgenteMenor24h = false,
                DireccionEntrega = "Jr. Huancavelica 320, Cercado - Lima",
                SedeAlias = "Sede Central Cercado",
                EstadoOperativo = PedidoLogisticaRules.EstadoPorPreparar,
                EstadoOperativoTexto = PedidoLogisticaRules.TextoPorPreparar,
                AlmacenOrigen = "Central Huachipa",
                Embalaje = "Cajas Reforzadas",
                VentaAprobada = true,
                Items = new List<PedidoItemViewModel>
                {
                    new() { Descripcion = "Botas de Seguridad Industrial Bata Industrials Titan", Cantidad = 50, UnidadMedida = "Pares" },
                    new() { Descripcion = "Lentes de Seguridad 3M Virtua Plus Antirrayadura", Cantidad = 80, UnidadMedida = "Und" }
                }
            }
        };

        return Task.FromResult(lista);
    }
}
