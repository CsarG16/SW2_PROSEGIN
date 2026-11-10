using System.ComponentModel.DataAnnotations;

namespace Prosegin.Web.ViewModels.CuentasPersonal;

public class CuentaPersonalListViewModel
{
    public string? Busqueda { get; set; }
    public string Rol { get; set; } = "Todos";
    public string Estado { get; set; } = "Todos";
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; }
    public int TotalRegistros { get; set; }
    public List<CuentaPersonalItemViewModel> Cuentas { get; set; } = new();
}

public class CuentaPersonalItemViewModel
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string CorreoElectronico { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activa { get; set; }
}

public class CuentaPersonalFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Complete los campos obligatorios")]
    [StringLength(150)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Complete los campos obligatorios")]
    [RegularExpression(@"^[a-zA-Z0-9._-]{3,50}$", ErrorMessage = "Ingrese un nombre de usuario válido")]
    public string NombreUsuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "Complete los campos obligatorios")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido")]
    public string CorreoElectronico { get; set; } = string.Empty;

    [Required(ErrorMessage = "Complete los campos obligatorios")]
    public string Rol { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden")]
    [DataType(DataType.Password)]
    public string? ConfirmarPassword { get; set; }
}
