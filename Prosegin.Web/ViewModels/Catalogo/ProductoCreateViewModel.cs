using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Prosegin.Web.ViewModels.Catalogo;

public class ProductoCreateViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Ingrese el código SKU del producto.")]
    [StringLength(50, ErrorMessage = "El código SKU no puede superar los 50 caracteres.")]
    [Display(Name = "Código SKU / Parte")]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese el nombre del producto EPP.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    [Display(Name = "Nombre comercial")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione una categoría de EPP.")]
    [StringLength(100, ErrorMessage = "La categoría no puede superar los 100 caracteres.")]
    [Display(Name = "Categoría EPP")]
    public string Categoria { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione una unidad de medida.")]
    [Display(Name = "Unidad de medida")]
    [StringLength(20, ErrorMessage = "La unidad no puede superar los 20 caracteres.")]
    public string UnidadMedida { get; set; } = "UND";

    [Required(ErrorMessage = "Seleccione una marca o fabricante.")]
    [Display(Name = "Marca / Fabricante")]
    [StringLength(100, ErrorMessage = "La marca no puede superar los 100 caracteres.")]
    public string? Marca { get; set; }

    [Required(ErrorMessage = "Ingrese el costo base de adquisición.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Ingrese un costo base mayor a cero.")]
    [Display(Name = "Costo base de adquisición")]
    public decimal CostoBaseAdquisicion { get; set; }

    [MinLength(1, ErrorMessage = "Seleccione al menos un proveedor autorizado.")]
    [Display(Name = "Proveedores autorizados")]
    public List<int> ProveedorIds { get; set; } = new();

    [BindNever]
    public List<ProveedorOpcionViewModel> ProveedoresDisponibles { get; set; } = new();

    [Required(ErrorMessage = "Adjunte la ficha técnica homologada en formato PDF.")]
    [Display(Name = "Ficha técnica (PDF)")]
    public IFormFile? FichaTecnica { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CostoBaseAdquisicion != decimal.Round(CostoBaseAdquisicion, 2))
        {
            yield return new ValidationResult("El costo base admite hasta 2 decimales.",
                new[] { nameof(CostoBaseAdquisicion) });
        }
    }
}

public class ProveedorOpcionViewModel
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
}
