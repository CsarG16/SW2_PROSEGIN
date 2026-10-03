using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.ViewModels.Catalogo;
using Prosegin.Web.Services;

namespace Prosegin.Web.Controllers;

public class CatalogoController : Controller
{
    private const string MensajeSkuDuplicado = "El código SKU ya se encuentra registrado";
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
    public async Task<IActionResult> RegistroProductos(CancellationToken cancellationToken)
    {
        var model = new ProductoCreateViewModel();
        await CargarProveedoresAsync(model, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ValidarSku(string sku, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return Json(new { valido = false, mensaje = "Ingrese un código SKU." });
        }

        var skuNormalizado = sku.Trim().ToUpperInvariant();
        var existe = await _context.Productos.AnyAsync(p => p.Sku == skuNormalizado, cancellationToken);
        if (existe)
        {
            return Json(new { valido = false, existe = true, mensaje = MensajeSkuDuplicado });
        }

        return Json(new { valido = true, existe = false, mensaje = "Código SKU disponible" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistroProductos(ProductoCreateViewModel model, CancellationToken cancellationToken)
    {
        if (model.FichaTecnica != null
            && !await FichaTecnicaValidator.EsValidaAsync(model.FichaTecnica, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.FichaTecnica), FichaTecnicaValidator.MensajeError);
        }

        var proveedorIds = model.ProveedorIds.Distinct().ToList();
        var proveedores = await _context.Proveedores
            .Where(p => proveedorIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
        if (proveedorIds.Count == 0 || proveedores.Count != proveedorIds.Count)
        {
            ModelState.AddModelError(nameof(model.ProveedorIds), "Seleccione al menos un proveedor autorizado de la lista.");
        }

        if (!string.IsNullOrWhiteSpace(model.Sku))
        {
            var skuNormalizado = model.Sku.Trim().ToUpperInvariant();
            if (await _context.Productos.AnyAsync(p => p.Sku == skuNormalizado, cancellationToken))
            {
                ModelState.AddModelError(nameof(model.Sku), MensajeSkuDuplicado);
            }
        }

        if (!ModelState.IsValid)
        {
            await CargarProveedoresAsync(model, cancellationToken);
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

        // 2. Persistir en MySQL con la ruta relativa pública
        var producto = new Producto
        {
            Sku = model.Sku.Trim().ToUpperInvariant(),
            Nombre = model.Nombre.Trim().ToUpperInvariant(),
            Categoria = model.Categoria.Trim(),
            UnidadMedida = string.IsNullOrWhiteSpace(model.UnidadMedida) ? "UND" : model.UnidadMedida.Trim().ToUpperInvariant(),
            CostoReferencial = model.CostoBaseAdquisicion,
            RutaFichaTecnicaPdf = $"/uploads/fichas/{nombreUnico}",
            NombreArchivoPdf = Path.GetFileName(model.FichaTecnica!.FileName),
            Descripcion = string.IsNullOrWhiteSpace(model.Marca)
                ? string.Empty
                : $"Marca: {model.Marca.Trim()}",
            Proveedores = proveedores,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        var guardado = false;
        try
        {
            await using (var stream = new FileStream(rutaFisica, FileMode.CreateNew))
            {
                await model.FichaTecnica.CopyToAsync(stream, cancellationToken);
            }

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync(cancellationToken);
            guardado = true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            // El índice único también protege dos registros simultáneos del mismo SKU.
            if (!await _context.Productos.AsNoTracking().AnyAsync(p => p.Sku == producto.Sku, cancellationToken))
            {
                throw;
            }

            ModelState.AddModelError(nameof(model.Sku), MensajeSkuDuplicado);
            await CargarProveedoresAsync(model, cancellationToken);
            return View(model);
        }
        finally
        {
            if (!guardado && System.IO.File.Exists(rutaFisica))
            {
                System.IO.File.Delete(rutaFisica);
            }
        }

        TempData["SuccessMessage"] = "Producto registrado con éxito";
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

    private async Task CargarProveedoresAsync(ProductoCreateViewModel model, CancellationToken cancellationToken)
    {
        model.ProveedoresDisponibles = await _context.Proveedores.AsNoTracking()
            .OrderBy(p => p.RazonSocial)
            .Select(p => new ProveedorOpcionViewModel { Id = p.Id, RazonSocial = p.RazonSocial, Ruc = p.Ruc })
            .ToListAsync(cancellationToken);
    }
}
