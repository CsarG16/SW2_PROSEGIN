using System.ComponentModel.DataAnnotations;

namespace Prosegin.Web.ViewModels.Clientes
{
    public class PuntoEntregaViewModel
    {
        public int Id { get; set; }

        public int ClienteId { get; set; }

        [Required(ErrorMessage = "El Tipo de Sede es obligatorio.")]
        [Display(Name = "Tipo de Sede")]
        public string TipoSede { get; set; } = string.Empty;

        [Required(ErrorMessage = "El Nombre o Alias de la Sede es obligatorio.")]
        [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
        [Display(Name = "Nombre o Alias de la Sede")]
        public string NombreAlias { get; set; } = string.Empty;

        [Required(ErrorMessage = "La Dirección Detallada es obligatoria.")]
        [StringLength(250, ErrorMessage = "La dirección no puede superar los 250 caracteres.")]
        [Display(Name = "Dirección Detallada")]
        public string Direccion { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Referencia de Llegada")]
        public string? Referencia { get; set; }

        [Required(ErrorMessage = "El Departamento es obligatorio.")]
        [Display(Name = "Departamento")]
        public string Departamento { get; set; } = "Lima";

        [Required(ErrorMessage = "La Provincia es obligatoria.")]
        [Display(Name = "Provincia")]
        public string Provincia { get; set; } = "Lima";

        [Required(ErrorMessage = "El Distrito es obligatorio.")]
        [Display(Name = "Distrito")]
        public string Distrito { get; set; } = string.Empty;

        [Required(ErrorMessage = "El código de Ubigeo es obligatorio.")]
        [StringLength(10, MinimumLength = 6, ErrorMessage = "El código de Ubigeo debe tener 6 dígitos.")]
        [Display(Name = "Ubigeo (6 Dígitos)")]
        public string Ubigeo { get; set; } = string.Empty;

        [Display(Name = "Contacto en Recepción / Almacén")]
        public string? ContactoRecepcion { get; set; }

        [Display(Name = "Teléfono Móvil")]
        public string? TelefonoMovil { get; set; }

        [Display(Name = "Horario Recepción")]
        public string? HorarioRecepcion { get; set; }

        [Display(Name = "Restricciones de Acceso / EPP")]
        public string? RestriccionesAcceso { get; set; }

        [Display(Name = "Marcar como dirección predeterminada")]
        public bool EsPredeterminada { get; set; }

        public bool Activo { get; set; } = true;
    }
}
