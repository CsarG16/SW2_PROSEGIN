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
        var hoy = DateTime.Today;
        var anio = hoy.Year;

        static string FormatearFecha(DateTime fecha)
        {
            var culture = new System.Globalization.CultureInfo("es-PE");
            var texto = fecha.ToString("dd MMM yyyy", culture).Replace(".", "");
            var partes = texto.Split(' ');
            if (partes.Length == 3 && partes[1].Length > 0)
            {
                partes[1] = char.ToUpper(partes[1][0]) + partes[1][1..];
                return string.Join(" ", partes);
            }
            return texto;
        }

        var lista = new List<PedidoDetalleViewModel>
        {
            // 1. Urgente: HOY 16:00 hrs (Entrega más próxima #1)
            new PedidoDetalleViewModel
            {
                Id = 1,
                NumeroPedido = $"PED-{anio}-0842",
                NumeroOrdenCompra = "OC-77821",
                ClienteRazonSocial = "Consorcio Vial Andino S.R.C.",
                ClienteRuc = "20492817263",
                FechaConfirmacion = hoy.AddDays(-2).AddHours(10).AddMinutes(14),
                FechaEntrega = hoy.AddHours(16),
                FechaConfirmacionTexto = $"{FormatearFecha(hoy.AddDays(-2))} 10:14 hrs",
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

            // 2. Urgente: Mañana 09:30 hrs (Entrega más próxima #2)
            new PedidoDetalleViewModel
            {
                Id = 2,
                NumeroPedido = $"PED-{anio}-0839",
                NumeroOrdenCompra = "OC-77810",
                ClienteRazonSocial = "Constructora e Ingeniería Minera del Sur S.A.C.",
                ClienteRuc = "20601928471",
                FechaConfirmacion = hoy.AddDays(-2).AddHours(8).AddMinutes(45),
                FechaEntrega = hoy.AddDays(1).AddHours(9).AddMinutes(30),
                FechaConfirmacionTexto = $"{FormatearFecha(hoy.AddDays(-2))} 08:45 hrs",
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

            // 3. Entrega en 2 días
            new PedidoDetalleViewModel
            {
                Id = 3,
                NumeroPedido = $"PED-{anio}-0831",
                NumeroOrdenCompra = "OC-77785",
                ClienteRazonSocial = "Minera del Norte S.A.C.",
                ClienteRuc = "20554189312",
                FechaConfirmacion = hoy.AddDays(-3).AddHours(16).AddMinutes(30),
                FechaEntrega = hoy.AddDays(2).AddHours(14),
                FechaConfirmacionTexto = $"{FormatearFecha(hoy.AddDays(-3))} 16:30 hrs",
                FechaEntregaTexto = FormatearFecha(hoy.AddDays(2)),
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

            // 4. Entrega en 3 días
            new PedidoDetalleViewModel
            {
                Id = 4,
                NumeroPedido = $"PED-{anio}-0824",
                NumeroOrdenCompra = "OC-77742",
                ClienteRazonSocial = "Constructora del Pacífico S.A.C.",
                ClienteRuc = "20112839401",
                FechaConfirmacion = hoy.AddDays(-4).AddHours(11).AddMinutes(20),
                FechaEntrega = hoy.AddDays(3).AddHours(11),
                FechaConfirmacionTexto = $"{FormatearFecha(hoy.AddDays(-4))} 11:20 hrs",
                FechaEntregaTexto = FormatearFecha(hoy.AddDays(3)),
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

            // 5. Entrega en 4 días
            new PedidoDetalleViewModel
            {
                Id = 5,
                NumeroPedido = $"PED-{anio}-0845",
                NumeroOrdenCompra = "OC-77830",
                ClienteRazonSocial = "Constructora e Ingeniería Minera del Sur S.A.C.",
                ClienteRuc = "20601928471",
                FechaConfirmacion = hoy.AddDays(-2).AddHours(14).AddMinutes(10),
                FechaEntrega = hoy.AddDays(4).AddHours(10),
                FechaConfirmacionTexto = $"{FormatearFecha(hoy.AddDays(-2))} 14:10 hrs",
                FechaEntregaTexto = FormatearFecha(hoy.AddDays(4)),
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

            // 6. Entrega en 6 días
            new PedidoDetalleViewModel
            {
                Id = 6,
                NumeroPedido = $"PED-{anio}-0835",
                NumeroOrdenCompra = "OC-77790",
                ClienteRazonSocial = "Minera del Norte S.A.C.",
                ClienteRuc = "20554189312",
                FechaConfirmacion = hoy.AddDays(-3).AddHours(18).AddMinutes(0),
                FechaEntrega = hoy.AddDays(6).AddHours(12),
                FechaConfirmacionTexto = $"{FormatearFecha(hoy.AddDays(-3))} 18:00 hrs",
                FechaEntregaTexto = FormatearFecha(hoy.AddDays(6)),
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

            // 7. Entrega en 7 días
            new PedidoDetalleViewModel
            {
                Id = 7,
                NumeroPedido = $"PED-{anio}-0820",
                NumeroOrdenCompra = "OC-77730",
                ClienteRazonSocial = "Constructora del Pacífico S.A.C.",
                ClienteRuc = "20112839401",
                FechaConfirmacion = hoy.AddDays(-5).AddHours(15).AddMinutes(30),
                FechaEntrega = hoy.AddDays(7).AddHours(16),
                FechaConfirmacionTexto = $"{FormatearFecha(hoy.AddDays(-5))} 15:30 hrs",
                FechaEntregaTexto = FormatearFecha(hoy.AddDays(7)),
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

            // 8. Entrega en 9 días
            new PedidoDetalleViewModel
            {
                Id = 8,
                NumeroPedido = $"PED-{anio}-0848",
                NumeroOrdenCompra = "OC-77850",
                ClienteRazonSocial = "Consorcio Vial Andino S.R.C.",
                ClienteRuc = "20492817263",
                FechaConfirmacion = hoy.AddDays(-1).AddHours(9).AddMinutes(0),
                FechaEntrega = hoy.AddDays(9).AddHours(11),
                FechaConfirmacionTexto = $"{FormatearFecha(hoy.AddDays(-1))} 09:00 hrs",
                FechaEntregaTexto = FormatearFecha(hoy.AddDays(9)),
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
