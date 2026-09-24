using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Prosegin.Web.ViewModels.Catalogo;

public class ProductoCreateViewModel
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

    [Display(Name = "Unidad de medida")]
    [StringLength(20, ErrorMessage = "La unidad no puede superar los 20 caracteres.")]
    public string UnidadMedida { get; set; } = "UND";

    [Required(ErrorMessage = "Ingrese el costo base de adquisición.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Ingrese un costo base mayor a cero.")]
    [Display(Name = "Costo base de adquisición")]
    public decimal CostoBaseAdquisicion { get; set; }

    [Required(ErrorMessage = "Ingrese la razón social del proveedor autorizado.")]
    [StringLength(200, ErrorMessage = "El proveedor no puede superar los 200 caracteres.")]
    [Display(Name = "Proveedor autorizado")]
    public string ProveedorAutorizado { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adjunte la ficha técnica homologada en formato PDF.")]
    [Display(Name = "Ficha técnica (PDF)")]
    public IFormFile? FichaTecnica { get; set; }
}