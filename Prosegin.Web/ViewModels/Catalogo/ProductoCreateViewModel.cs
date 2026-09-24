using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Prosegin.Web.ViewModels.Catalogo;

public class ProductoCreateViewModel
{
    [Required(ErrorMessage = "Ingrese el código SKU.")]
    [StringLength(50, ErrorMessage = "El código SKU no puede superar los 50 caracteres.")]
    [Display(Name = "Código SKU")]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese el nombre del producto.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione una categoría EPP.")]
    [StringLength(100, ErrorMessage = "La categoría no puede superar los 100 caracteres.")]
    [Display(Name = "Categoría EPP")]
    public string Categoria { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese el costo base de adquisición.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Ingrese un costo mayor a cero.")]
    [Display(Name = "Costo base de adquisición")]
    public decimal CostoBaseAdquisicion { get; set; }

    [Required(ErrorMessage = "Ingrese el proveedor autorizado.")]
    [StringLength(200, ErrorMessage = "El proveedor no puede superar los 200 caracteres.")]
    [Display(Name = "Proveedor autorizado")]
    public string ProveedorAutorizado { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adjunte la ficha técnica homologada en formato PDF.")]
    [Display(Name = "Ficha técnica del fabricante")]
    public IFormFile? FichaTecnica { get; set; }
}