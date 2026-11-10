using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Prosegin.Web.Services;

namespace Prosegin.Web.Controllers;

public class AccesoController : Controller
{
    private readonly ICuentasPersonalService _service;

    public AccesoController(ICuentasPersonalService service)
    {
        _service = service;
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string usuario, string password, string? returnUrl, CancellationToken cancellationToken)
    {
        var resultado = await _service.ValidarAccesoAsync(usuario.Trim(), password, cancellationToken);
        if (!resultado.Exito)
        {
            TempData["ToastError"] = resultado.Mensaje;
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, resultado.NombreUsuario!),
            new Claim("NombreCompleto", resultado.NombreCompleto!),
            new Claim(ClaimTypes.Role, resultado.Rol!)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
