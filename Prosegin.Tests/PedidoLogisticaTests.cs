using Prosegin.Data.Validation;
using Xunit;

namespace Prosegin.Tests;

public class PedidoTestDto
{
    public int Id { get; set; }
    public string NumeroPedido { get; set; } = string.Empty;
    public string NumeroOrdenCompra { get; set; } = string.Empty;
    public string ClienteRazonSocial { get; set; } = string.Empty;
    public string ClienteRuc { get; set; } = string.Empty;
    public DateTime FechaConfirmacion { get; set; }
    public DateTime FechaEntrega { get; set; }
    public bool EsUrgenteMenor24h { get; set; }
    public string EstadoOperativo { get; set; } = PedidoLogisticaRules.EstadoPorPreparar;
    public bool VentaAprobada { get; set; } = true;
    public List<string> Items { get; set; } = new();
}

public class PedidoLogisticaTests
{
    private List<PedidoTestDto> ObtenerPedidosPrueba()
    {
        var hoy = DateTime.Today;
        var anio = hoy.Year;

        return new List<PedidoTestDto>
        {
            new()
            {
                Id = 1,
                NumeroPedido = $"PED-{anio}-0842",
                NumeroOrdenCompra = "OC-77821",
                ClienteRazonSocial = "Consorcio Vial Andino S.R.C.",
                ClienteRuc = "20492817263",
                FechaConfirmacion = hoy.AddDays(-2).AddHours(10),
                FechaEntrega = hoy.AddHours(16), // HOY 16:00 (Urgente < 24h)
                EsUrgenteMenor24h = true,
                EstadoOperativo = PedidoLogisticaRules.EstadoPorPreparar,
                VentaAprobada = true,
                Items = new List<string> { "Casco Dieléctrico Tipo II 120 Und" }
            },
            new()
            {
                Id = 2,
                NumeroPedido = $"PED-{anio}-0839",
                NumeroOrdenCompra = "OC-77810",
                ClienteRazonSocial = "Constructora e Ingeniería Minera del Sur S.A.C.",
                ClienteRuc = "20601928471",
                FechaConfirmacion = hoy.AddDays(-2).AddHours(8),
                FechaEntrega = hoy.AddDays(1).AddHours(9), // Mañana 09:00 (Urgente < 24h)
                EsUrgenteMenor24h = true,
                EstadoOperativo = PedidoLogisticaRules.EstadoEnPreparacion,
                VentaAprobada = true
            },
            new()
            {
                Id = 3,
                NumeroPedido = $"PED-{anio}-0831",
                NumeroOrdenCompra = "OC-77785",
                ClienteRazonSocial = "Minera del Norte S.A.C.",
                ClienteRuc = "20554189312",
                FechaConfirmacion = hoy.AddDays(-3).AddHours(16),
                FechaEntrega = hoy.AddDays(3),
                EsUrgenteMenor24h = false,
                EstadoOperativo = PedidoLogisticaRules.EstadoEnPreparacion,
                VentaAprobada = true
            },
            new()
            {
                Id = 4,
                NumeroPedido = $"PED-{anio}-0824",
                NumeroOrdenCompra = "OC-77742",
                ClienteRazonSocial = "Constructora del Pacífico S.A.C.",
                ClienteRuc = "20112839401",
                FechaConfirmacion = hoy.AddDays(-4).AddHours(11),
                FechaEntrega = hoy.AddDays(5),
                EsUrgenteMenor24h = false,
                EstadoOperativo = PedidoLogisticaRules.EstadoListoDespacho,
                VentaAprobada = true
            },
            new()
            {
                Id = 5,
                NumeroPedido = $"PED-{anio}-0899",
                NumeroOrdenCompra = "", // Sin orden de compra asignada
                ClienteRazonSocial = "Empresa Borrador S.A.C.",
                ClienteRuc = "20999999999",
                FechaConfirmacion = hoy.AddDays(-1).AddHours(12),
                FechaEntrega = hoy.AddDays(2),
                EsUrgenteMenor24h = false,
                EstadoOperativo = PedidoLogisticaRules.EstadoPorPreparar,
                VentaAprobada = false // Cotización no aprobada
            }
        };
    }

    [Fact]
    public void ExcluirCotizacionesEnBorradorOSinOrdenCompra()
    {
        var pedidos = ObtenerPedidosPrueba();
        var anio = DateTime.Today.Year;

        var validos = pedidos
            .Where(p => PedidoLogisticaRules.EsPedidoValidoParaLogistica(p.VentaAprobada, p.NumeroOrdenCompra))
            .ToList();

        Assert.Equal(4, validos.Count);
        Assert.DoesNotContain(validos, p => p.NumeroPedido == $"PED-{anio}-0899");
    }

    [Fact]
    public void OrdenarPedidos_EntregaMasProximaPrimero_Y_DesempatePorConfirmacion()
    {
        var hoy = DateTime.Today;
        var p1 = new PedidoTestDto
        {
            NumeroPedido = "P1",
            FechaEntrega = hoy.AddDays(2),
            FechaConfirmacion = hoy.AddDays(-2).AddHours(14)
        };
        var p2 = new PedidoTestDto
        {
            NumeroPedido = "P2",
            FechaEntrega = hoy.AddDays(1), // Más próxima
            FechaConfirmacion = hoy.AddDays(-1).AddHours(10)
        };
        var p3 = new PedidoTestDto
        {
            NumeroPedido = "P3",
            FechaEntrega = hoy.AddDays(2), // Mismo día que P1
            FechaConfirmacion = hoy.AddDays(-2).AddHours(9) // Confirmado ANTES que P1
        };

        var ordenados = new[] { p1, p2, p3 }
            .OrderBy(p => p.FechaEntrega)
            .ThenBy(p => p.FechaConfirmacion)
            .ToList();

        Assert.Equal("P2", ordenados[0].NumeroPedido); // Entrega más próxima
        Assert.Equal("P3", ordenados[1].NumeroPedido); // Desempate por fecha confirmación más antigua
        Assert.Equal("P1", ordenados[2].NumeroPedido);
    }

    [Fact]
    public void Busqueda_PorNumeroPedido_FiltraCorrectamente()
    {
        var anio = DateTime.Today.Year;
        var pedidos = ObtenerPedidosPrueba()
            .Where(p => PedidoLogisticaRules.EsPedidoValidoParaLogistica(p.VentaAprobada, p.NumeroOrdenCompra))
            .ToList();

        var resultado = pedidos
            .Where(p => PedidoLogisticaRules.CoincideBusqueda(p.NumeroPedido, p.NumeroOrdenCompra, p.ClienteRuc, p.ClienteRazonSocial, "0842"))
            .ToList();

        Assert.Single(resultado);
        Assert.Equal($"PED-{anio}-0842", resultado[0].NumeroPedido);
    }

    [Fact]
    public void Busqueda_PorRuc_FiltraCorrectamente()
    {
        var pedidos = ObtenerPedidosPrueba()
            .Where(p => PedidoLogisticaRules.EsPedidoValidoParaLogistica(p.VentaAprobada, p.NumeroOrdenCompra))
            .ToList();

        var resultado = pedidos
            .Where(p => PedidoLogisticaRules.CoincideBusqueda(p.NumeroPedido, p.NumeroOrdenCompra, p.ClienteRuc, p.ClienteRazonSocial, "20492817263"))
            .ToList();

        Assert.Single(resultado);
        Assert.Equal("Consorcio Vial Andino S.R.C.", resultado[0].ClienteRazonSocial);
    }

    [Fact]
    public void Busqueda_PorRazonSocial_ParcialEInsensibleMayusculas()
    {
        var anio = DateTime.Today.Year;
        var pedidos = ObtenerPedidosPrueba()
            .Where(p => PedidoLogisticaRules.EsPedidoValidoParaLogistica(p.VentaAprobada, p.NumeroOrdenCompra))
            .ToList();

        var resultado = pedidos
            .Where(p => PedidoLogisticaRules.CoincideBusqueda(p.NumeroPedido, p.NumeroOrdenCompra, p.ClienteRuc, p.ClienteRazonSocial, "minera"))
            .ToList();

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, p => p.NumeroPedido == $"PED-{anio}-0839");
        Assert.Contains(resultado, p => p.NumeroPedido == $"PED-{anio}-0831");
    }

    [Fact]
    public void FiltroPorFaseOperativa_SegmentaCargaDeTrabajo()
    {
        var pedidos = ObtenerPedidosPrueba()
            .Where(p => PedidoLogisticaRules.EsPedidoValidoParaLogistica(p.VentaAprobada, p.NumeroOrdenCompra))
            .ToList();

        var porPreparar = pedidos.Where(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoPorPreparar).ToList();
        var enPreparacion = pedidos.Where(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoEnPreparacion).ToList();
        var listoDespacho = pedidos.Where(p => p.EstadoOperativo == PedidoLogisticaRules.EstadoListoDespacho).ToList();

        Assert.Single(porPreparar);
        Assert.Equal(2, enPreparacion.Count);
        Assert.Single(listoDespacho);
    }

    [Fact]
    public void AlertaUrgente_DetectaMenor24Horas()
    {
        var anio = DateTime.Today.Year;
        var referencia = new DateTime(anio, 10, 2, 10, 0, 0);

        var entregaHoy = new DateTime(anio, 10, 2, 16, 0, 0);
        var entregaMananaTemprano = new DateTime(anio, 10, 3, 9, 30, 0); // 23.5 horas después
        var entregaLejana = new DateTime(anio, 10, 6, 10, 0, 0);

        Assert.True(PedidoLogisticaRules.EsEntregaMenor24Horas(entregaHoy, referencia));
        Assert.True(PedidoLogisticaRules.EsEntregaMenor24Horas(entregaMananaTemprano, referencia));
        Assert.False(PedidoLogisticaRules.EsEntregaMenor24Horas(entregaLejana, referencia));
    }

    [Fact]
    public void Busqueda_SinCoincidencias_DevuelveVacioYMensajeEstablecido()
    {
        var pedidos = ObtenerPedidosPrueba()
            .Where(p => PedidoLogisticaRules.EsPedidoValidoParaLogistica(p.VentaAprobada, p.NumeroOrdenCompra))
            .ToList();

        var resultado = pedidos
            .Where(p => PedidoLogisticaRules.CoincideBusqueda(p.NumeroPedido, p.NumeroOrdenCompra, p.ClienteRuc, p.ClienteRazonSocial, "TERMINO_INEXISTENTE_999"))
            .ToList();

        Assert.Empty(resultado);
        Assert.Equal("No se encontraron pedidos confirmados con los criterios ingresados", PedidoLogisticaRules.MensajeSinResultados);
    }
}
