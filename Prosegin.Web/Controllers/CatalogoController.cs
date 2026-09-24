using Microsoft.AspNetCore.Hosting;
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
    private readonly IWebHostEnvironment _environment;

    public CatalogoController(ProseginDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        var productos = await _context.Productos
            .Where(p => p.Activo)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        return View(productos);
    }

    [HttpGet]
    public IActionResult RegistroProductos()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistroProductos(ProductoCreateViewModel model)
    {
        ValidarFichaTecnica(model.FichaTecnica);

        if (await _context.Productos.AnyAsync(p => p.Sku == model.Sku.Trim()))
        {
            ModelState.AddModelError(nameof(model.Sku), "El código SKU ya se encuentra registrado en el catálogo.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 1. Guardar físicamente el PDF en wwwroot/uploads/fichas/
        var carpetaFichas = Path.Combine(_environment.WebRootPath, "uploads", "fichas");
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
            Nombre = model.Nombre.Trim(),
            Categoria = model.Categoria,
            CostoReferencial = model.CostoBaseAdquisicion,
            RutaFichaTecnicaPdf = $"/uploads/fichas/{nombreUnico}",
            NombreArchivoPdf = model.FichaTecnica.FileName,
            Descripcion = $"Proveedor: {model.ProveedorAutorizado}",
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"El producto '{producto.Nombre}' ({producto.Sku}) fue registrado exitosamente con su ficha técnica.";
        return RedirectToAction(nameof(Index));
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