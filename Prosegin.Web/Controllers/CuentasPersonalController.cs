using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prosegin.Web.Services;
using Prosegin.Web.ViewModels.CuentasPersonal;

namespace Prosegin.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class CuentasPersonalController : Controller
{
    private readonly ICuentasPersonalService _service;

    public CuentasPersonalController(ICuentasPersonalService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? busqueda, string? rol, string? estado, int pagina = 1, CancellationToken cancellationToken = default)
    {
        var model = await _service.ObtenerAsync(busqueda, rol, estado, pagina, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id, CancellationToken cancellationToken)
    {
        var model = await _service.ObtenerFormularioAsync(id, cancellationToken);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Form(CuentaPersonalFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var mensaje = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                ?? "Complete los campos obligatorios";
            TempData["ToastError"] = mensaje;
            return View(model);
        }

        var resultado = await _service.GuardarAsync(model, cancellationToken);
        if (!resultado.Exito)
        {
            TempData["ToastError"] = resultado.Mensaje;
            return View(model);
        }

        TempData["ToastSuccess"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id, CancellationToken cancellationToken)
    {
        var resultado = await _service.DesactivarAsync(id, User.Identity?.Name, cancellationToken);
        TempData[resultado.Exito ? "ToastSuccess" : "ToastError"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
