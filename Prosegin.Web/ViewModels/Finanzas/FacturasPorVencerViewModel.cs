namespace Prosegin.Web.ViewModels.Finanzas;

public class FacturasPorVencerViewModel
{
    public int? ClienteId { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string Estado { get; set; } = "POR_VENCER";
    public string? Mensaje { get; set; }
    public int PorVencerSieteDias { get; set; }
    public int PorVencerQuinceDias { get; set; }
    public int PorVencerTreintaDias { get; set; }
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int TotalRegistros { get; set; }
    public List<ClienteFiltroFacturaViewModel> Clientes { get; set; } = new();
    public List<FacturaPorVencerItemViewModel> Facturas { get; set; } = new();
}

public class ClienteFiltroFacturaViewModel
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class FacturaPorVencerItemViewModel
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public DateTime FechaVencimiento { get; set; }
    public int DiasHastaVencimiento { get; set; }
    public decimal Monto { get; set; }
    public decimal SaldoPendiente { get; set; }
    public string EstadoPago { get; set; } = string.Empty;
    public bool PdfDisponible { get; set; }
}

public class RegistroPagosViewModel
{
    public string Busqueda { get; set; } = string.Empty;
    public int? FacturaIdSeleccionada { get; set; }
    public FacturaPendientePagoViewModel? FacturaSeleccionada { get; set; }
    public string? Mensaje { get; set; }
    public bool BusquedaInvalida { get; set; }
    public DateTime FechaPagoPredeterminada { get; set; } = Prosegin.Web.HoraPeru.Hoy;
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int TotalFacturas { get; set; }
    public List<FacturaPendientePagoViewModel> Facturas { get; set; } = new();
}

public class FacturaPendientePagoViewModel
{
    public int Id { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string RucCliente { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int PlazoDias { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal MontoAbonado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int CantidadAbonos { get; set; }
    public List<AbonoFacturaViewModel> Abonos { get; set; } = new();
}

public class AbonoFacturaViewModel
{
    public int NumeroCuota { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaPago { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string? Referencia { get; set; }
}

public class CuentasPorCobrarViewModel
{
    public int? ClienteId { get; set; }
    public string Estado { get; set; } = "TODAS";
    public DateTime? FechaEmisionDesde { get; set; }
    public DateTime? FechaEmisionHasta { get; set; }
    public DateTime? FechaVencimientoDesde { get; set; }
    public DateTime? FechaVencimientoHasta { get; set; }
    public string? Mensaje { get; set; }
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int TotalRegistros { get; set; }
    public List<ClienteFiltroFacturaViewModel> Clientes { get; set; } = new();
    public List<CuentaPorCobrarItemViewModel> Facturas { get; set; } = new();
}

public class CuentaPorCobrarItemViewModel
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string RucCliente { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int PlazoDias { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal MontoAbonado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool PdfDisponible { get; set; }
    public List<DetalleFacturaCuentaViewModel> Detalles { get; set; } = new();
}

public class DetalleCuentaPorCobrarViewModel
{
    public int Id { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string RucCliente { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int PlazoDias { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal SaldoPendiente { get; set; }
    public string Estado { get; set; } = string.Empty;
    public List<DetalleFacturaCuentaViewModel> Detalles { get; set; } = new();
}

public class DetalleFacturaCuentaViewModel
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
}

public class RegistrarPagoRequest
{
    public int FacturaId { get; set; }
    public decimal? Monto { get; set; }
    public DateTime? FechaPago { get; set; }
    public string? MetodoPago { get; set; }
    public string? Referencia { get; set; }
    public string? Busqueda { get; set; }
    public int Pagina { get; set; } = 1;
}
