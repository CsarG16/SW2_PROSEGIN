using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Prosegin.Web.Services;

namespace Prosegin.Web.Controllers;

public class AccesoController : Controller
{
    private const int MaxIntentosFallidos = 5;
    private static readonly TimeSpan VentanaBloqueo = TimeSpan.FromMinutes(15);

    private readonly ICuentasPersonalService _service;
    private readonly IMemoryCache _cache;

    public AccesoController(ICuentasPersonalService service, IMemoryCache cache)
    {
        _service = service;
        _cache = cache;
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
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string? usuario, string? password, string? returnUrl, CancellationToken cancellationToken)
    {
        usuario = usuario?.Trim() ?? string.Empty;
        password ??= string.Empty;

        // Bloqueo temporal tras varios intentos fallidos consecutivos del mismo usuario.
        var claveIntentos = $"login-fallos:{usuario.ToLowerInvariant()}";
        if (_cache.TryGetValue(claveIntentos, out int fallos) && fallos >= MaxIntentosFallidos)
        {
            TempData["ToastError"] = "Demasiados intentos fallidos. Intente nuevamente en unos minutos";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        var resultado = await _service.ValidarAccesoAsync(usuario, password, cancellationToken);
        if (!resultado.Exito)
        {
            _cache.Set(claveIntentos, fallos + 1, VentanaBloqueo);
            TempData["ToastError"] = resultado.Mensaje;
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        _cache.Remove(claveIntentos);

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
