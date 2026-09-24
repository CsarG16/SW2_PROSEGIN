using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Prosegin.Web.ViewModels.Catalogo;

namespace Prosegin.Web.Controllers;

public class CatalogoController : Controller
{
    private const long MaxFichaTecnicaBytes = 5 * 1024 * 1024;
    private readonly IWebHostEnvironment _environment;

    public CatalogoController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult RegistroProductos()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RegistroProductos(ProductoCreateViewModel model)
    {
        ValidarFichaTecnica(model.FichaTecnica);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var carpetaFichas = Path.Combine(_environment.WebRootPath, "uploads", "fichas-tecnicas");
        Directory.CreateDirectory(carpetaFichas);

        var nombreArchivo = $"{Guid.NewGuid():N}.pdf";
        var rutaArchivo = Path.Combine(carpetaFichas, nombreArchivo);
        using (var stream = System.IO.File.Create(rutaArchivo))
        {
            model.FichaTecnica!.CopyTo(stream);
        }

        TempData["SuccessMessage"] = $"El producto '{model.Nombre}' está listo para ser registrado.";
        return RedirectToAction(nameof(RegistroProductos));
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