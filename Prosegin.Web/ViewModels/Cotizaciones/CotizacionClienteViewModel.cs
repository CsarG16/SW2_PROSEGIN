using System.ComponentModel.DataAnnotations;

namespace Prosegin.Web.ViewModels.Cotizaciones;

public class CotizacionClienteViewModel
{
    public int ClienteId { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string? RepresentanteLegal { get; set; }
    public string EstadoSunat { get; set; } = string.Empty;
    public string CondicionSunat { get; set; } = string.Empty;
    public decimal LimiteCredito { get; set; }
    public decimal SaldoPendiente { get; set; }
    public decimal SaldoVencido { get; set; }
    public int DiasMora { get; set; }
    public bool ClienteBloqueado { get; set; }
    public string? MotivoBloqueo { get; set; }
    public bool DatosSunatDesactualizados { get; set; }

    [Required]
    public string CondicionPago { get; set; } = "Contado";

    public decimal Total { get; set; }

    public List<ProductoCotizacionViewModel> ProductosDisponibles { get; set; } = new();
}

public class ProductoCotizacionViewModel
{
    public int ProductoId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = "UND";
    public decimal CostoReferencial { get; set; }
    public int StockDisponible { get; set; }
    public bool Seleccionado { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a cero.")]
    public int Cantidad { get; set; } = 1;

    [Range(typeof(decimal), "0", "100", ErrorMessage = "El margen debe estar entre 0 y 100 %.")]
    public decimal MargenPorcentaje { get; set; } = 30m;
}
