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

        [StringLength(100)]
        [Display(Name = "Departamento")]
        public string? Departamento { get; set; }

        [StringLength(100)]
        [Display(Name = "Provincia")]
        public string? Provincia { get; set; }

        [StringLength(100)]
        [Display(Name = "Distrito")]
        public string? Distrito { get; set; }

        [StringLength(6, ErrorMessage = "El código de Ubigeo debe tener 6 dígitos.")]
        [Display(Name = "Código Ubigeo")]
        public string? Ubigeo { get; set; }

        [Display(Name = "Estado SUNAT")]
        public string EstadoSunat { get; set; } = "ACTIVO";

        [Display(Name = "Condición SUNAT")]
        public string CondicionSunat { get; set; } = "HABIDO";

        [StringLength(200, ErrorMessage = "El representante legal no puede exceder los 200 caracteres.")]
        [Display(Name = "Representante Legal")]
        public string? RepresentanteLegal { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El límite de crédito no puede ser negativo.")]
        [Display(Name = "Límite de Crédito")]
        public decimal LimiteCredito { get; set; }
    }
}
