using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Prosegin.Web.ViewModels.Clientes
{
    public class ClienteCreateViewModel
    {
        [Required(ErrorMessage = "El RUC es obligatorio.")]
        [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "Número de RUC inválido")]
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

        [StringLength(250, ErrorMessage = "La referencia no puede exceder los 250 caracteres.")]
        [Display(Name = "Referencia de Ubicación")]
        public string? Referencia { get; set; }

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
        public string EstadoSunat { get; set; } = "NO VERIFICADO";

        [Display(Name = "Condición SUNAT")]
        public string CondicionSunat { get; set; } = "NO VERIFICADO";

        public int? ClienteId { get; set; }

        public List<ContactoClienteViewModel> Contactos { get; set; } = new();
    }

    public class ContactoClienteViewModel
    {
        [Required(ErrorMessage = "Los nombres y apellidos son obligatorios.")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El cargo es obligatorio.")]
        [StringLength(100)]
        public string Cargo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono celular es obligatorio.")]
        [RegularExpression(@"^9[0-9]{8}$", ErrorMessage = "El teléfono debe tener 9 dígitos y comenzar con 9.")]
        public string Telefono { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        [StringLength(150)]
        public string Correo { get; set; } = string.Empty;

        public bool EsPrincipal { get; set; }
    }
}
