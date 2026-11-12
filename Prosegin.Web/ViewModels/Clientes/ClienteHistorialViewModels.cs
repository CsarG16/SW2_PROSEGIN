using Prosegin.Data.Entities;

namespace Prosegin.Web.ViewModels.Clientes;

/// <summary>
/// ViewModel para la vista de historial comercial exclusivo por empresa cliente.
/// </summary>
public class ClienteHistorialViewModel
{
    public Cliente Cliente { get; set; } = null!;
    public bool SoloCompras { get; set; }
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int TotalRegistros { get; set; }
    public string? Mensaje { get; set; }
    public List<ClienteHistorialDocumentoViewModel> Registros { get; set; } = new();
}

/// <summary>
/// Representa una operación comercial histórica (Cotización o Compra formalizada) de un cliente.
/// </summary>
public class ClienteHistorialDocumentoViewModel
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public string Moneda { get; set; } = "PEN";
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string EstadoCss { get; set; } = "badge-status-neutral";
    public bool PdfDisponible { get; set; }
    public string CondicionesPago { get; set; } = string.Empty;
    public string CondicionesEntrega { get; set; } = string.Empty;
    public string? NumeroOrdenCompraCliente { get; set; }
    public string? ReferenciaOrigen { get; set; }
    public List<ClienteHistorialItemViewModel> Items { get; set; } = new();
}

/// <summary>
/// Ítem EPP pactado en un documento histórico (estricta lectura para auditoría comercial).
/// </summary>
public class ClienteHistorialItemViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = "UND";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
}

