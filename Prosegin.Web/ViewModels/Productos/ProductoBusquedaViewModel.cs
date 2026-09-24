namespace Prosegin.Web.ViewModels.Productos;

public class ProductoBusquedaViewModel
{
    public string CriterioBusqueda { get; set; } = string.Empty;
    public string? CategoriaSeleccionada { get; set; }
    public string Vista { get; set; } = "tabla"; // "tabla" o "grid"
    public List<string> CategoriasDisponibles { get; set; } = new();
    public List<ProductoResultadoViewModel> Resultados { get; set; } = new();
    public bool SeRealizoBusqueda { get; set; }
    public string MensajeNoResultados { get; set; } = string.Empty;
}

public class ProductoResultadoViewModel
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal CostoReferencial { get; set; }
    public int Stock { get; set; }
    public bool TieneFichaTecnica { get; set; }
    public string? RutaFichaTecnicaPdf { get; set; }
}