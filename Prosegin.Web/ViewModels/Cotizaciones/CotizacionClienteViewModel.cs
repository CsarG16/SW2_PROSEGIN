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

    [Range(0.01, double.MaxValue, ErrorMessage = "Ingrese un total mayor a cero.")]
    public decimal Total { get; set; }
}
