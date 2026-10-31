namespace Prosegin.Web.ViewModels.Pedidos;

public class PedidoDetalleViewModel
{
    public int Id { get; set; }
    public string NumeroPedido { get; set; } = string.Empty;
    public string NumeroOrdenCompra { get; set; } = string.Empty;
    public string ClienteRazonSocial { get; set; } = string.Empty;
    public string ClienteRuc { get; set; } = string.Empty;

    public DateTime FechaConfirmacion { get; set; }
    public DateTime FechaEntrega { get; set; }
    public string FechaConfirmacionTexto { get; set; } = string.Empty;
    public string FechaEntregaTexto { get; set; } = string.Empty;
    public bool EsUrgenteMenor24h { get; set; }

    public string DireccionEntrega { get; set; } = string.Empty;
    public string SedeAlias { get; set; } = string.Empty;

    /// <summary>
    /// Fases operativas: POR_PREPARAR, EN_PREPARACION, LISTO_DESPACHO
    /// </summary>
    public string EstadoOperativo { get; set; } = "POR_PREPARAR";
    public string EstadoOperativoTexto { get; set; } = "Por Preparar";

    public string AlmacenOrigen { get; set; } = "Central Huachipa";
    public string Embalaje { get; set; } = "Packs Paletizados";

    public bool VentaAprobada { get; set; } = true;
    public bool PuedeGenerarOrdenesCompra { get; set; }
    public bool PuedeGenerarFactura { get; set; }
    public bool FacturaEmitida { get; set; }
    public string? NumeroFactura { get; set; }
    public string? EstadoEmisionFactura { get; set; }
    public string? EnlacePdfFactura { get; set; }
    public bool PuedeGenerarGuiaRemision { get; set; }
    public bool GuiaRemisionEmitida { get; set; }
    public string? NumeroGuiaRemision { get; set; }
    public string? EnlacePdfGuia { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public string CondicionPago { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string ClienteCorreoPrincipal { get; set; } = string.Empty;
    public List<PedidoContactoViewModel> Destinatarios { get; set; } = new();

    public List<PedidoItemViewModel> Items { get; set; } = new();
}

public class PedidoContactoViewModel
{
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; }
}
