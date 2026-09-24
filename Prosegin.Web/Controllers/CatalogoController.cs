using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Web.ViewModels.Catalogo;

namespace Prosegin.Web.Controllers;

public class CatalogoController : Controller
{
    private readonly ProseginDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public CatalogoController(ProseginDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? codigoProducto, string? categoria)
    {
        var query = _context.Productos
            .AsNoTracking()
            .Where(p => p.Activo)
            .AsQueryable();

        var criteriosValidos = !string.IsNullOrWhiteSpace(codigoProducto) || !string.IsNullOrWhiteSpace(categoria);

        if (!string.IsNullOrWhiteSpace(codigoProducto))
        {
            var termino = codigoProducto.Trim();
            query = query.Where(p =>
                p.Sku.Contains(termino)
                || p.Nombre.Contains(termino));
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            query = query.Where(p => p.Categoria == categoria.Trim());
        }

        var productos = await query
            .OrderBy(p => p.Nombre)
            .Select(p => new ProductoCatalogoItemViewModel
            {
                Id = p.Id,
                Sku = p.Sku,
                Nombre = p.Nombre,
                Categoria = p.Categoria,
                Precio = p.CostoReferencial,
                StockDisponible = p.StockDisponible,
                RutaFichaTecnicaPdf = p.RutaFichaTecnicaPdf,
                NombreArchivoPdf = p.NombreArchivoPdf
            })
            .ToListAsync();

        var categorias = await _context.Productos
            .AsNoTracking()
            .Where(p => p.Activo)
            .Select(p => p.Categoria)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        var model = new CatalogoIndexViewModel
        {
            CodigoProducto = codigoProducto,
            CategoriaSeleccionada = categoria,
            Categorias = categorias,
            Productos = productos,
            MensajeSinResultados = criteriosValidos && productos.Count == 0
                ? "NO SE ENCONTRARON PRODUCTOS REGISTRADOS PARA LOS CRITERIOS INGRESADOS"
                : null
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> VerFicha(int id)
    {
        var producto = await _context.Productos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);

        if (producto == null || string.IsNullOrWhiteSpace(producto.RutaFichaTecnicaPdf))
        {
            return NotFound();
        }

        var filePath = ResolvePdfPath(producto.RutaFichaTecnicaPdf);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var fileName = string.IsNullOrWhiteSpace(producto.NombreArchivoPdf)
            ? "ficha-tecnica.pdf"
            : producto.NombreArchivoPdf;

        return PhysicalFile(filePath, "application/pdf", fileName);
    }

    private string ResolvePdfPath(string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return relativeOrAbsolutePath;
        }

        var candidates = new[]
        {
            Path.Combine(_webHostEnvironment.WebRootPath ?? string.Empty, relativeOrAbsolutePath.TrimStart('/','\\')),
            Path.Combine(_webHostEnvironment.ContentRootPath, relativeOrAbsolutePath.TrimStart('/','\\')),
            Path.Combine(_webHostEnvironment.WebRootPath ?? string.Empty, "uploads", "fichas", relativeOrAbsolutePath.TrimStart('/','\\'))
        };

        return candidates.FirstOrDefault(System.IO.File.Exists) ?? candidates[0];
    }
}
