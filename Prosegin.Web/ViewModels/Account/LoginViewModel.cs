using System.ComponentModel.DataAnnotations;

namespace Prosegin.Web.ViewModels.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [Display(Name = "Usuario")]
    public string NombreUsuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Mantener sesión activa")]
    public bool Recordarme { get; set; }
}
