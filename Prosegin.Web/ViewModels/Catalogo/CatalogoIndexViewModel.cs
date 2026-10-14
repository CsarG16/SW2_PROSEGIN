namespace Prosegin.Web.ViewModels.Catalogo;

public class CatalogoIndexViewModel
{
    public string? CodigoProducto { get; set; }
    public string? CategoriaSeleccionada { get; set; }
    public List<string> Categorias { get; set; } = new();
    public List<ProductoCatalogoItemViewModel> Productos { get; set; } = new();
    public string? MensajeSinResultados { get; set; }
}

public class ProductoCatalogoItemViewModel
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int StockDisponible { get; set; }
    public string? RutaFichaTecnicaPdf { get; set; }
    public string? NombreArchivoPdf { get; set; }
    public string? RutaImagen { get; set; }
    public List<ProveedorTarifaCatalogoViewModel> TarifasProveedor { get; set; } = new();
}

public class ProveedorTarifaCatalogoViewModel
{
    public int ProveedorId { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public decimal CostoCompra { get; set; }
    public int PlazoEntregaHoras { get; set; }
    public bool EsPrincipal { get; set; }
}
