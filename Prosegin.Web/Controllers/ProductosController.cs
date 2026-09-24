using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.ViewModels.Productos;

namespace Prosegin.Web.Controllers;

public class ProductosController : Controller
{
    private readonly ProseginDbContext _context;

    public ProductosController(ProseginDbContext context)
    {
        _context = context;
    }

    // GET: Productos
    public async Task<IActionResult> Index(string criterioBusqueda, string? categoriaSeleccionada, string vista = "tabla")
    {
        var viewModel = new ProductoBusquedaViewModel
        {
            CriterioBusqueda = criterioBusqueda ?? string.Empty,
            CategoriaSeleccionada = categoriaSeleccionada,
            Vista = vista,
            CategoriasDisponibles = await _context.Productos
                .Where(p => p.Activo)
                .Select(p => p.Categoria)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync()
        };

        // Si hay criterios de búsqueda, realizar la búsqueda
        if (!string.IsNullOrWhiteSpace(criterioBusqueda) || !string.IsNullOrWhiteSpace(categoriaSeleccionada))
        {
            var query = _context.Productos.Where(p => p.Activo);

            // Búsqueda por código (SKU) o nombre del producto (coincidencias parciales)
            if (!string.IsNullOrWhiteSpace(criterioBusqueda))
            {
                var criterio = criterioBusqueda.Trim().ToLower();
                query = query.Where(p => 
                    p.Sku.ToLower().Contains(criterio) || 
                    p.Nombre.ToLower().Contains(criterio));
            }

            // Filtrado por categoría exacta
            if (!string.IsNullOrWhiteSpace(categoriaSeleccionada))
            {
                query = query.Where(p => p.Categoria == categoriaSeleccionada);
            }

            var productos = await query
                .OrderBy(p => p.Sku)
                .ToListAsync();

            viewModel.Resultados = productos.Select(p => new ProductoResultadoViewModel
            {
                Id = p.Id,
                Sku = p.Sku,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                UnidadMedida = p.UnidadMedida,
                CostoReferencial = p.CostoReferencial,
                Stock = p.Stock,
                TieneFichaTecnica = !string.IsNullOrEmpty(p.RutaFichaTecnicaPdf),
                RutaFichaTecnicaPdf = p.RutaFichaTecnicaPdf
            }).ToList();

            viewModel.SeRealizoBusqueda = true;

            // Mensaje si no hay resultados
            if (!viewModel.Resultados.Any())
            {
                viewModel.MensajeNoResultados = "NO SE ENCONTRARON PRODUCTOS REGISTRADOS PARA LOS CRITERIOS INGRESADOS";
            }
        }

        return View(viewModel);
    }

    // GET: Productos/VerFichaTecnica/5
    public IActionResult VerFichaTecnica(int id)
    {
        var producto = _context.Productos.Find(id);
        if (producto == null || string.IsNullOrEmpty(producto.RutaFichaTecnicaPdf))
        {
            return NotFound();
        }

        // El archivo PDF se almacena en wwwroot, la ruta es relativa
        return Redirect($"/{producto.RutaFichaTecnicaPdf.TrimStart('/')}");
    }
}