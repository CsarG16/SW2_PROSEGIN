using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.ViewModels.Catalogo;

namespace Prosegin.Web.Controllers;

public class CatalogoController : Controller
{
    private const long MaxFichaTecnicaBytes = 5 * 1024 * 1024;
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
            .OrderByDescending(p => p.Id)
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
    public async Task<IActionResult> RegistroProductos()
    {
        ViewBag.ProductosRegistrados = await _context.Productos
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderByDescending(p => p.Id)
            .Take(10)
            .ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistroProductos(ProductoCreateViewModel model)
    {
        ValidarFichaTecnica(model.FichaTecnica);

        if (!string.IsNullOrWhiteSpace(model.Sku))
        {
            var skuNormalizado = model.Sku.Trim().ToUpperInvariant();
            if (await _context.Productos.AnyAsync(p => p.Sku == skuNormalizado && p.Activo))
            {
                ModelState.AddModelError(nameof(model.Sku), $"El código SKU '{skuNormalizado}' ya se encuentra registrado en el catálogo.");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ProductosRegistrados = await _context.Productos
                .AsNoTracking()
                .Where(p => p.Activo)
                .OrderByDescending(p => p.Id)
                .Take(10)
                .ToListAsync();

            return View(model);
        }

        // 1. Guardar físicamente el PDF en wwwroot/uploads/fichas/
        var webRoot = _webHostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
        }

        var carpetaFichas = Path.Combine(webRoot, "uploads", "fichas");
        Directory.CreateDirectory(carpetaFichas);

        var nombreUnico = $"{Guid.NewGuid():N}.pdf";
        var rutaFisica = Path.Combine(carpetaFichas, nombreUnico);

        await using (var stream = new FileStream(rutaFisica, FileMode.Create))
        {
            await model.FichaTecnica!.CopyToAsync(stream);
        }

        // 2. Persistir en MySQL con la ruta relativa pública
        var producto = new Producto
        {
            Sku = model.Sku.Trim().ToUpperInvariant(),
            Nombre = model.Nombre.Trim().ToUpperInvariant(),
            Categoria = model.Categoria.Trim(),
            UnidadMedida = string.IsNullOrWhiteSpace(model.UnidadMedida) ? "UND" : model.UnidadMedida.Trim().ToUpperInvariant(),
            CostoReferencial = model.CostoBaseAdquisicion,
            RutaFichaTecnicaPdf = $"/uploads/fichas/{nombreUnico}",
            NombreArchivoPdf = model.FichaTecnica.FileName,
            Descripcion = $"Proveedor: {model.ProveedorAutorizado.Trim()}",
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"El producto '{producto.Nombre}' ({producto.Sku}) fue registrado exitosamente en la base de datos con su ficha técnica.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> VerFicha(int id)
    {
        var producto = await _context.Productos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);

        if (producto == null || string.IsNullOrWhiteSpace(producto.RutaFichaTecnicaPdf))
        {
            return NotFound("El producto no cuenta con ficha técnica registrada.");
        }

        var filePath = ResolvePdfPath(producto.RutaFichaTecnicaPdf);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound($"El archivo de la ficha técnica no se encuentra disponible: {Path.GetFileName(producto.RutaFichaTecnicaPdf)}");
        }

        var fileName = string.IsNullOrWhiteSpace(producto.NombreArchivoPdf)
            ? Path.GetFileName(filePath)
            : producto.NombreArchivoPdf;

        // Configurar Content-Disposition inline para visualización directa en el navegador
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
        return PhysicalFile(filePath, "application/pdf");
    }

    private string ResolvePdfPath(string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return string.Empty;
        }

        // Si ya es una ruta física absoluta existente (con unidad tipo C:\... o UNC)
        if (Path.IsPathFullyQualified(relativeOrAbsolutePath) && System.IO.File.Exists(relativeOrAbsolutePath))
        {
            return Path.GetFullPath(relativeOrAbsolutePath);
        }

        var webRoot = !string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath)
            ? _webHostEnvironment.WebRootPath
            : Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");

        var cleanPath = relativeOrAbsolutePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fileNameOnly = Path.GetFileName(cleanPath);

        var candidates = new[]
        {
            Path.Combine(webRoot, cleanPath),
            Path.Combine(webRoot, "fichas", fileNameOnly),
            Path.Combine(webRoot, "uploads", "fichas", fileNameOnly),
            Path.Combine(_webHostEnvironment.ContentRootPath, cleanPath),
            Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", cleanPath),
            Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "fichas", fileNameOnly)
        };

        var found = candidates.FirstOrDefault(System.IO.File.Exists);
        return found != null ? Path.GetFullPath(found) : Path.GetFullPath(candidates[0]);
    }

    private void ValidarFichaTecnica(IFormFile? fichaTecnica)
    {
        if (fichaTecnica is null || fichaTecnica.Length == 0)
        {
            return;
        }

        var extension = Path.GetExtension(fichaTecnica.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(ProductoCreateViewModel.FichaTecnica), "La ficha técnica debe estar en formato PDF.");
        }

        if (fichaTecnica.Length > MaxFichaTecnicaBytes)
        {
            ModelState.AddModelError(nameof(ProductoCreateViewModel.FichaTecnica), "La ficha técnica no puede superar los 5 MB.");
        }
    }
}
