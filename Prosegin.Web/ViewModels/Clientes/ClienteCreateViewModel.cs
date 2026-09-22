using System.ComponentModel.DataAnnotations;

namespace Prosegin.Web.ViewModels.Clientes
{
    public class ClienteCreateViewModel
    {
        [Required(ErrorMessage = "El RUC es obligatorio.")]
        [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "El RUC debe tener exactamente 11 dígitos numéricos.")]
        [Display(Name = "Número de RUC")]
        public string Ruc { get; set; } = string.Empty;

        [Required(ErrorMessage = "La Razón Social es obligatoria.")]
        [StringLength(200, ErrorMessage = "La Razón Social no puede exceder los 200 caracteres.")]
        [Display(Name = "Razón Social / Denominación Legal")]
        public string RazonSocial { get; set; } = string.Empty;

        [Required(ErrorMessage = "La Dirección Fiscal es obligatoria.")]
        [StringLength(250, ErrorMessage = "La Dirección Fiscal no puede exceder los 250 caracteres.")]
        [Display(Name = "Dirección Fiscal Completa")]
        public string DireccionFiscal { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "El teléfono no puede exceder los 20 caracteres.")]
        [Display(Name = "Teléfono de Contacto Fiscal")]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        [StringLength(100, ErrorMessage = "El correo no puede exceder los 100 caracteres.")]
        [Display(Name = "Correo Electrónico")]
        public string? CorreoElectronico { get; set; }
    }
}
