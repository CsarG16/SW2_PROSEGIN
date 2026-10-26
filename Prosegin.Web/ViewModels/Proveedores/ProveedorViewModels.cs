using System.ComponentModel.DataAnnotations;

namespace Prosegin.Web.ViewModels.Proveedores;

public class ProveedorBusquedaViewModel
{
    public string Termino { get; set; } = string.Empty;
    public int Pagina { get; set; } = 1;
    public int TamanoPagina { get; set; } = 20;
    public int TotalResultados { get; set; }
    public int TotalPaginas { get; set; } = 1;
    public List<ProveedorListaItemViewModel> Resultados { get; set; } = new();

    public int Desde => TotalResultados == 0 ? 0 : (Pagina - 1) * TamanoPagina + 1;
    public int Hasta => TotalResultados == 0 ? 0 : Desde + Resultados.Count - 1;
}

public class ProveedorListaItemViewModel
{
    public int Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string Especialidad { get; set; } = string.Empty;
    public string CondicionPago { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string CorreoComercial { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string EstadoSunat { get; set; } = "NO VERIFICADO";
    public string CondicionSunat { get; set; } = "NO VERIFICADO";
    public string DireccionFiscal { get; set; } = string.Empty;
    public List<ContactoProveedorViewModel> Contactos { get; set; } = new();
    public List<CuentaBancariaProveedorViewModel> CuentasBancarias { get; set; } = new();
}

public class ProveedorCreateViewModel
{
    public int? Id { get; set; }
    public string EstadoSunat { get; set; } = "NO VERIFICADO";
    public string CondicionSunat { get; set; } = "NO VERIFICADO";

    [Required(ErrorMessage = "El RUC es obligatorio.")]
    [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "El RUC debe tener 11 dígitos.")]
    [Display(Name = "RUC")]
    public string Ruc { get; set; } = string.Empty;

    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(200)]
    [Display(Name = "Razón social")]
    public string RazonSocial { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Nombre comercial / abreviatura")]
    public string NombreComercial { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona la especialidad o línea de EPP.")]
    [StringLength(200)]
    [Display(Name = "Especialidad / líneas de EPP suministradas")]
    public string Especialidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona la condición de crédito o pago.")]
    [StringLength(100)]
    [Display(Name = "Condición de crédito / pago habitual")]
    public string CondicionPago { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección fiscal es obligatoria.")]
    [StringLength(250)]
    [Display(Name = "Dirección fiscal")]
    public string DireccionFiscal { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono comercial es obligatorio.")]
    [StringLength(20)]
    [Display(Name = "Teléfono comercial")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo comercial es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [StringLength(100)]
    [Display(Name = "Correo comercial")]
    public string CorreoComercial { get; set; } = string.Empty;

    public List<ContactoProveedorViewModel> Contactos { get; set; } = new() { new() };
    public List<CuentaBancariaProveedorViewModel> CuentasBancarias { get; set; } = new() { new() };
}

public class ContactoProveedorViewModel
{
    [Required(ErrorMessage = "El nombre del contacto es obligatorio.")]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El cargo es obligatorio.")]
    [StringLength(100)]
    public string Cargo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono del contacto es obligatorio.")]
    [RegularExpression(@"^9[0-9]{8}$", ErrorMessage = "El teléfono debe tener 9 dígitos y comenzar con 9.")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo del contacto es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [StringLength(150)]
    public string Correo { get; set; } = string.Empty;

    public bool EsPrincipal { get; set; }
}

public class CuentaBancariaProveedorViewModel
{
    [Required(ErrorMessage = "Selecciona el banco.")]
    [StringLength(100)]
    public string Banco { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona la moneda de la cuenta.")]
    [RegularExpression("^(PEN|USD)$", ErrorMessage = "Selecciona PEN o USD.")]
    public string Moneda { get; set; } = "PEN";

    [StringLength(30)]
    [RegularExpression(@"^[0-9-]{0,30}$", ErrorMessage = "La cuenta debe contener solo números y guiones.")]
    public string? NumeroCuenta { get; set; }

    [Required(ErrorMessage = "El CCI es obligatorio.")]
    [RegularExpression(@"^[0-9]{20}$", ErrorMessage = "El CCI debe tener 20 dígitos.")]
    public string Cci { get; set; } = string.Empty;

    public bool EsPrincipal { get; set; }
}
