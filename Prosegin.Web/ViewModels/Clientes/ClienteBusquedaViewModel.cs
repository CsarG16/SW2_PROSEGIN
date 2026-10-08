using Prosegin.Web.Services;

namespace Prosegin.Web.ViewModels.Clientes;

public class ClienteBusquedaViewModel
{
    public string Termino { get; set; } = string.Empty;
    public string? Mensaje { get; set; }
    public string? MensajeActualizacionSunat { get; set; }
    public List<ClienteBusquedaItemViewModel> Resultados { get; set; } = new();
    public SunatConsultaResult? ConsultaSunat { get; set; }
}

public class ClienteBusquedaItemViewModel
{
    public int Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string? Ubigeo { get; set; }
    public string? Departamento { get; set; }
    public string? Provincia { get; set; }
    public string? Distrito { get; set; }
    public string EstadoSunat { get; set; } = string.Empty;
    public string CondicionSunat { get; set; } = string.Empty;
    public decimal SaldoVencido { get; set; }
    public int DiasMora { get; set; }
    public bool CreditoExcedido { get; set; }
    public bool ClienteBloqueado { get; set; }
    public string EstadoCredito { get; set; } = string.Empty;
    public string? MensajeBloqueo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string TipoCliente { get; set; } = "Cliente Corporativo";
    public int CantidadSedes { get; set; } = 1;
}
