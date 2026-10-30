using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Data.Validation;
using Prosegin.Web.Services;
using Prosegin.Web.ViewModels.Proveedores;

namespace Prosegin.Web.Controllers;

public class ProveedoresController : Controller
{
    private readonly ProseginDbContext _context;
    private readonly ISunatService _sunatService;
    private readonly ILogger<ProveedoresController> _logger;

    public ProveedoresController(
        ProseginDbContext context,
        ISunatService sunatService,
        ILogger<ProveedoresController> logger)
    {
        _context = context;
        _sunatService = sunatService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? termino,
        int pagina = 1,
        int tamanoPagina = 20,
        int? proveedorNuevoId = null,
        CancellationToken cancellationToken = default)
    {
        var tamanosPermitidos = new[] { 10, 20, 50 };
        if (!tamanosPermitidos.Contains(tamanoPagina))
        {
            tamanoPagina = 20;
        }

        var model = new ProveedorBusquedaViewModel
        {
            Termino = termino?.Trim() ?? string.Empty,
            Pagina = Math.Max(1, pagina),
            TamanoPagina = tamanoPagina
        };
        IQueryable<Proveedor> proveedores = _context.Proveedores
            .AsNoTracking()
            .AsQueryable();

        List<int>? idsCoincidentes = null;
        if (!string.IsNullOrWhiteSpace(model.Termino))
        {
            var candidatos = await proveedores
                .Select(p => new { p.Id, p.Ruc, p.RazonSocial, p.NombreComercial })
                .ToListAsync(cancellationToken);
            idsCoincidentes = candidatos
                .Where(p => ContieneTexto(p.Ruc, model.Termino)
                    || ContieneTexto(p.RazonSocial, model.Termino)
                    || ContieneTexto(p.NombreComercial, model.Termino))
                .OrderBy(p => p.Id == proveedorNuevoId ? 0 : 1)
                .ThenBy(p => p.RazonSocial, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Id)
                .Select(p => p.Id)
                .ToList();
            model.TotalResultados = idsCoincidentes.Count;
        }
        else
        {
            model.TotalResultados = await proveedores.CountAsync(cancellationToken);
        }
        model.TotalPaginas = Math.Max(1, (int)Math.Ceiling(model.TotalResultados / (double)model.TamanoPagina));
        model.Pagina = Math.Min(model.Pagina, model.TotalPaginas);
        IQueryable<Proveedor> consultaPagina = proveedores;
        int[]? idsPagina = null;
        if (idsCoincidentes != null)
        {
            idsPagina = idsCoincidentes
                .Skip((model.Pagina - 1) * model.TamanoPagina)
                .Take(model.TamanoPagina)
                .ToArray();
            consultaPagina = consultaPagina.Where(p => idsPagina.Contains(p.Id));
        }
        else
        {
            consultaPagina = consultaPagina
                .OrderBy(p => p.Id == proveedorNuevoId ? 0 : 1)
                .ThenBy(p => p.RazonSocial)
                .ThenBy(p => p.Id)
                .Skip((model.Pagina - 1) * model.TamanoPagina)
                .Take(model.TamanoPagina);
        }

        var registros = await consultaPagina
            .Include(p => p.Contactos)
            .Include(p => p.CuentasBancarias)
            .ToListAsync(cancellationToken);
        if (idsPagina != null)
        {
            var orden = idsPagina
                .Select((id, index) => new { id, index })
                .ToDictionary(item => item.id, item => item.index);
            registros = registros.OrderBy(p => orden[p.Id]).ToList();
        }

        model.Resultados = registros.Select(p => new ProveedorListaItemViewModel
            {
                Id = p.Id,
                Ruc = p.Ruc,
                RazonSocial = p.RazonSocial,
                NombreComercial = p.NombreComercial,
                Especialidad = p.Especialidad,
                CondicionPago = p.CondicionPago,
                Telefono = p.Telefono,
                CorreoComercial = p.CorreoComercial,
                Activo = p.Activo,
                EstadoSunat = p.EstadoSunat,
                CondicionSunat = p.CondicionSunat,
                DireccionFiscal = string.IsNullOrWhiteSpace(p.DireccionFiscal) ? p.UbicacionMalvinas : p.DireccionFiscal,
                Contactos = p.Contactos.Count > 0
                    ? p.Contactos.OrderByDescending(c => c.EsPrincipal).ThenBy(c => c.Nombre)
                        .Select(c => new ContactoProveedorViewModel
                        {
                            Nombre = c.Nombre,
                            Cargo = c.Cargo,
                            Telefono = c.Telefono,
                            Correo = c.Correo,
                            EsPrincipal = c.EsPrincipal
                        }).ToList()
                    : string.IsNullOrWhiteSpace(p.Contacto)
                        ? new List<ContactoProveedorViewModel>()
                        : new List<ContactoProveedorViewModel>
                        {
                            new()
                            {
                                Nombre = p.Contacto,
                                Telefono = p.Telefono,
                                Correo = p.CorreoComercial,
                                EsPrincipal = true
                            }
                        },
                CuentasBancarias = p.CuentasBancarias.OrderBy(c => c.Banco)
                    .Select(c => new CuentaBancariaProveedorViewModel
                    {
                        Banco = c.Banco,
                        Moneda = c.Moneda,
                        NumeroCuenta = c.NumeroCuenta,
                        Cci = c.Cci,
                        EsPrincipal = c.EsPrincipal
                    }).ToList()
            })
            .ToList();

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Registrar proveedor";
        return View("Form", new ProveedorCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProveedorCreateViewModel model, CancellationToken cancellationToken)
    {
        model.Ruc = model.Ruc?.Trim() ?? string.Empty;
        if (!RucValidator.ValidarFormato(model.Ruc, out var errorRuc))
        {
            ModelState.AddModelError(nameof(model.Ruc), errorRuc ?? "Número de RUC inválido.");
        }

        if (await _context.Proveedores.AnyAsync(p => p.Ruc == model.Ruc, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Ruc), "Ya existe un proveedor registrado con este RUC.");
        }

        ValidarContactosYCuentas(model);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Registrar proveedor";
            return View("Form", model);
        }

        var consulta = await _sunatService.ConsultarRucAsync(model.Ruc, cancellationToken);
        if (!consulta.Success || !consulta.EsActivo || !consulta.EsHabido
            || string.IsNullOrWhiteSpace(consulta.RazonSocial)
            || string.IsNullOrWhiteSpace(consulta.DireccionFiscal))
        {
            ModelState.AddModelError(nameof(model.Ruc),
                consulta.Success
                    ? "El proveedor debe figurar como ACTIVO y HABIDO en SUNAT, y la consulta debe incluir razón social y dirección fiscal."
                    : consulta.Message ?? "No se pudo verificar el RUC en SUNAT. Intenta nuevamente.");
            ViewData["Title"] = "Registrar proveedor";
            return View("Form", model);
        }

        if (await _context.Proveedores.AnyAsync(p => p.Ruc == model.Ruc, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Ruc), "Ya existe un proveedor registrado con este RUC.");
            ViewData["Title"] = "Registrar proveedor";
            return View("Form", model);
        }

        var proveedor = new Proveedor
        {
            Ruc = model.Ruc,
            RazonSocial = consulta.RazonSocial?.Trim() ?? model.RazonSocial.Trim(),
            NombreComercial = model.NombreComercial.Trim(),
            Especialidad = model.Especialidad.Trim(),
            CondicionPago = model.CondicionPago.Trim(),
            DireccionFiscal = consulta.DireccionFiscal?.Trim() ?? model.DireccionFiscal.Trim(),
            UbicacionMalvinas = consulta.DireccionFiscal?.Trim() ?? model.DireccionFiscal.Trim(),
            Telefono = model.Telefono.Trim(),
            CorreoComercial = model.CorreoComercial.Trim(),
            Contacto = model.Contactos.First(c => c.EsPrincipal || model.Contactos.Count == 1).Nombre.Trim(),
            EstadoSunat = consulta.Estado ?? "NO VERIFICADO",
            CondicionSunat = consulta.Condicion ?? "NO VERIFICADO",
            Contactos = CrearContactos(model.Contactos),
            CuentasBancarias = CrearCuentas(model.CuentasBancarias)
        };

        _context.Proveedores.Add(proveedor);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "No se pudo registrar el proveedor con RUC {Ruc}", model.Ruc);
            ModelState.AddModelError(nameof(model.Ruc), "Ocurrió un error al guardar el proveedor. Vuelve a intentarlo; si persiste, contacta soporte.");
            ViewData["Title"] = "Registrar proveedor";
            return View("Form", model);
        }

        return RedirectToAction(nameof(Confirmacion), new { id = proveedor.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Confirmacion(int id, CancellationToken cancellationToken)
    {
        var existe = await _context.Proveedores
            .AsNoTracking()
            .AnyAsync(p => p.Id == id, cancellationToken);
        return existe ? View(id) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var proveedor = await _context.Proveedores
            .AsNoTracking()
            .Include(p => p.Contactos)
            .Include(p => p.CuentasBancarias)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (proveedor == null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Editar proveedor";
        return View("Form", CrearModelo(proveedor));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProveedorCreateViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id != id)
        {
            return BadRequest();
        }

        ModelState.Remove(nameof(model.Ruc));
        ValidarContactosYCuentas(model);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar proveedor";
            return View("Form", model);
        }

        var proveedor = await _context.Proveedores
            .Include(p => p.Contactos)
            .Include(p => p.CuentasBancarias)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (proveedor == null)
        {
            return NotFound();
        }

        proveedor.RazonSocial = model.RazonSocial.Trim();
        proveedor.NombreComercial = model.NombreComercial.Trim();
        proveedor.Especialidad = model.Especialidad.Trim();
        proveedor.CondicionPago = model.CondicionPago.Trim();
        proveedor.DireccionFiscal = model.DireccionFiscal.Trim();
        proveedor.UbicacionMalvinas = model.DireccionFiscal.Trim();
        proveedor.Telefono = model.Telefono.Trim();
        proveedor.CorreoComercial = model.CorreoComercial.Trim();
        proveedor.Contacto = model.Contactos.First(c => c.EsPrincipal || model.Contactos.Count == 1).Nombre.Trim();
        _context.ContactosProveedor.RemoveRange(proveedor.Contactos);
        _context.CuentasBancariasProveedor.RemoveRange(proveedor.CuentasBancarias);
        proveedor.Contactos = CrearContactos(model.Contactos);
        proveedor.CuentasBancarias = CrearCuentas(model.CuentasBancarias);

        await _context.SaveChangesAsync(cancellationToken);
        TempData["ModalSuccess"] = "Proveedor actualizado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivate(int id, CancellationToken cancellationToken)
    {
        var proveedor = await _context.Proveedores.FindAsync([id], cancellationToken);
        if (proveedor == null)
        {
            return NotFound();
        }

        proveedor.Activo = false;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["ModalSuccess"] = "Proveedor inactivado. Se conserva para consultar su historial.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ConsultarSunat(string ruc, CancellationToken cancellationToken)
    {
        ruc = ruc?.Trim() ?? string.Empty;
        if (!RucValidator.ValidarFormato(ruc, out var error))
        {
            return BadRequest(new { message = error });
        }

        var consulta = await _sunatService.ConsultarRucAsync(ruc, cancellationToken);
        if (!consulta.Success)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = consulta.Message });
        }

        return Json(new
        {
            consulta.Ruc,
            consulta.RazonSocial,
            consulta.DireccionFiscal,
            consulta.Estado,
            consulta.Condicion,
            consulta.EsActivo,
            consulta.EsHabido
        });
    }

    private void ValidarContactosYCuentas(ProveedorCreateViewModel model)
    {
        for (var i = 0; i < model.Contactos.Count; i++)
        {
            var contacto = model.Contactos[i];
            if (string.IsNullOrWhiteSpace(contacto.Nombre)
                && string.IsNullOrWhiteSpace(contacto.Telefono)
                && string.IsNullOrWhiteSpace(contacto.Correo))
            {
                foreach (var propiedad in new[] { nameof(contacto.Nombre), nameof(contacto.Cargo), nameof(contacto.Telefono), nameof(contacto.Correo) })
                {
                    ModelState.Remove($"{nameof(model.Contactos)}[{i}].{propiedad}");
                }
            }
        }

        for (var i = 0; i < model.CuentasBancarias.Count; i++)
        {
            var cuenta = model.CuentasBancarias[i];
            if (string.IsNullOrWhiteSpace(cuenta.Banco)
                && string.IsNullOrWhiteSpace(cuenta.Moneda)
                && string.IsNullOrWhiteSpace(cuenta.NumeroCuenta)
                && string.IsNullOrWhiteSpace(cuenta.Cci))
            {
                foreach (var propiedad in new[] { nameof(cuenta.Banco), nameof(cuenta.Moneda), nameof(cuenta.Cci) })
                {
                    ModelState.Remove($"{nameof(model.CuentasBancarias)}[{i}].{propiedad}");
                }
            }
        }

        model.Contactos = model.Contactos.Where(c =>
                !string.IsNullOrWhiteSpace(c.Nombre)
                || !string.IsNullOrWhiteSpace(c.Telefono)
                || !string.IsNullOrWhiteSpace(c.Correo))
            .ToList();
        model.CuentasBancarias = model.CuentasBancarias.Where(c =>
                !string.IsNullOrWhiteSpace(c.Banco)
                || !string.IsNullOrWhiteSpace(c.NumeroCuenta)
                || !string.IsNullOrWhiteSpace(c.Cci))
            .ToList();

        if (model.Contactos.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Contactos), "Registra al menos una persona de contacto.");
        }
        else
        {
            var principales = model.Contactos.Where(c => c.EsPrincipal).ToList();
            if (principales.Count > 1)
            {
                ModelState.AddModelError(nameof(model.Contactos), "Solo puede haber un contacto principal.");
            }
            else if (principales.Count == 0)
            {
                model.Contactos[0].EsPrincipal = true;
            }
        }

        if (model.CuentasBancarias.Count == 0
            || model.CuentasBancarias.Any(c =>
                string.IsNullOrWhiteSpace(c.Banco)
                || (string.IsNullOrWhiteSpace(c.NumeroCuenta) && string.IsNullOrWhiteSpace(c.Cci))))
        {
            ModelState.AddModelError(nameof(model.CuentasBancarias),
                "Registra al menos un banco y un número de cuenta o CCI.");
        }

        var cuentasPrincipales = model.CuentasBancarias.Count(c => c.EsPrincipal);
        if (cuentasPrincipales > 1)
        {
            ModelState.AddModelError(nameof(model.CuentasBancarias), "Solo puede haber una cuenta bancaria principal.");
        }
        else if (model.CuentasBancarias.Count > 0 && cuentasPrincipales == 0)
        {
            model.CuentasBancarias[0].EsPrincipal = true;
        }
    }

    private static List<ContactoProveedor> CrearContactos(IEnumerable<ContactoProveedorViewModel> contactos)
    {
        return contactos.Select(c => new ContactoProveedor
        {
            Nombre = c.Nombre.Trim(),
            Cargo = c.Cargo.Trim(),
            Telefono = c.Telefono.Trim(),
            Correo = c.Correo.Trim(),
            EsPrincipal = c.EsPrincipal
        }).ToList();
    }

    private static List<CuentaBancariaProveedor> CrearCuentas(IEnumerable<CuentaBancariaProveedorViewModel> cuentas)
    {
        return cuentas.Select(c => new CuentaBancariaProveedor
        {
            Banco = c.Banco.Trim(),
            Moneda = c.Moneda,
            NumeroCuenta = c.NumeroCuenta?.Trim() ?? string.Empty,
            Cci = c.Cci?.Trim() ?? string.Empty,
            EsPrincipal = c.EsPrincipal
        }).ToList();
    }

    private static bool ContieneTexto(string? valor, string termino)
    {
        return !string.IsNullOrEmpty(valor)
            && valor.Contains(termino, StringComparison.OrdinalIgnoreCase);
    }

    private static ProveedorCreateViewModel CrearModelo(Proveedor proveedor)
    {
        return new ProveedorCreateViewModel
        {
            Id = proveedor.Id,
            Ruc = proveedor.Ruc,
            RazonSocial = proveedor.RazonSocial,
            NombreComercial = proveedor.NombreComercial,
            Especialidad = proveedor.Especialidad,
            CondicionPago = proveedor.CondicionPago,
            DireccionFiscal = string.IsNullOrWhiteSpace(proveedor.DireccionFiscal)
                ? proveedor.UbicacionMalvinas
                : proveedor.DireccionFiscal,
            Telefono = proveedor.Telefono,
            CorreoComercial = proveedor.CorreoComercial,
            EstadoSunat = proveedor.EstadoSunat,
            CondicionSunat = proveedor.CondicionSunat,
            Contactos = proveedor.Contactos.Count == 0
                ? new List<ContactoProveedorViewModel>
                {
                    new()
                    {
                        Nombre = proveedor.Contacto,
                        Telefono = proveedor.Telefono,
                        Correo = proveedor.CorreoComercial,
                        EsPrincipal = true
                    }
                }
                : proveedor.Contactos.Select(c => new ContactoProveedorViewModel
                {
                    Nombre = c.Nombre,
                    Cargo = c.Cargo,
                    Telefono = c.Telefono,
                    Correo = c.Correo,
                    EsPrincipal = c.EsPrincipal
                }).ToList(),
            CuentasBancarias = proveedor.CuentasBancarias.Count == 0
                ? new List<CuentaBancariaProveedorViewModel> { new() }
                : proveedor.CuentasBancarias.Select(c => new CuentaBancariaProveedorViewModel
                {
                    Banco = c.Banco,
                    Moneda = c.Moneda,
                    NumeroCuenta = c.NumeroCuenta,
                    Cci = c.Cci,
                    EsPrincipal = c.EsPrincipal
                }).ToList()
        };
    }
}
