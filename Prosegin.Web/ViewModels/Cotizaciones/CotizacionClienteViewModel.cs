using System.ComponentModel.DataAnnotations;
using Prosegin.Data.Validation;

namespace Prosegin.Web.ViewModels.Cotizaciones;

public class CotizacionClienteViewModel
{
    public int ClienteId { get; set; }
    public int? CotizacionId { get; set; }
    public string Correlativo { get; set; } = string.Empty;
    public DateTime? FechaEmision { get; set; }
    public string EstadoCotizacion { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string EstadoSunat { get; set; } = string.Empty;
    public string CondicionSunat { get; set; } = string.Empty;
    public decimal SaldoPendiente { get; set; }
    public decimal SaldoVencido { get; set; }
    public int DiasMora { get; set; }
    public bool ClienteBloqueado { get; set; }
    public string? MotivoBloqueo { get; set; }
    public bool DatosSunatDesactualizados { get; set; }

    [Required]
    public string CondicionPago { get; set; } = CondicionPagoRules.PlazoCreditoPredeterminado;

    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }

    public List<ProductoCotizacionViewModel> ProductosDisponibles { get; set; } = new();
    public List<ProductoCotizacionGuardadaViewModel> ProductosCotizacionGuardada { get; set; } = new();
}

public class ProductoCotizacionViewModel
{
    public int ProductoId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = "UND";
    public decimal CostoReferencial { get; set; }
    public decimal PrecioUnitario { get; set; }
    public int StockDisponible { get; set; }
    public string RutaImagen { get; set; } = string.Empty;
    public string? RutaFichaTecnicaPdf { get; set; }
    public string? NombreArchivoPdf { get; set; }
    public bool Seleccionado { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser un número entero mayor a cero.")]
    public int Cantidad { get; set; } = 1;

    [Range(typeof(decimal), "0", "1000", ErrorMessage = "El margen no puede ser negativo.")]
    public decimal MargenPorcentaje { get; set; } = 0m;
}

public class ProductoCotizacionGuardadaViewModel
{
    public int ProductoId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public string? RutaFichaTecnicaPdf { get; set; }
    public string? NombreArchivoPdf { get; set; }
}
