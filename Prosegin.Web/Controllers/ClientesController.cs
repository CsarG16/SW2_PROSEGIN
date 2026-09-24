using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.Services;
using Prosegin.Web.ViewModels.Clientes;

namespace Prosegin.Web.Controllers
{
    public class ClientesController : Controller
    {
        private readonly ProseginDbContext _context;
        private readonly ISunatService _sunatService;

        public ClientesController(ProseginDbContext context, ISunatService sunatService)
        {
            _context = context;
            _sunatService = sunatService;
        }

        // GET: Clientes/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.ClientesRegistrados = await _context.Clientes
                .OrderByDescending(c => c.Id)
                .Take(10)
                .ToListAsync();

            return View();
        }

        // POST: Clientes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClienteCreateViewModel model)
        {
            // 1. Validación estricta Módulo 11 oficial de SUNAT
            if (!_sunatService.ValidarFormatoRuc(model.Ruc, out var errorRuc))
            {
                ModelState.AddModelError("Ruc", errorRuc!);
            }

            if (ModelState.IsValid)
            {
                var rucLimpio = model.Ruc.Trim();

                // 2. Validación de RUC duplicado en base de datos
                var rucExiste = await _context.Clientes.AnyAsync(c => c.Ruc == rucLimpio);
                if (rucExiste)
                {
                    ModelState.AddModelError("Ruc", $"El RUC {rucLimpio} ya se encuentra registrado en el sistema.");
                    ViewBag.ClientesRegistrados = await _context.Clientes
                        .OrderByDescending(c => c.Id)
                        .Take(10)
                        .ToListAsync();
                    return View(model);
                }

                var cliente = new Cliente
                {
                    Ruc = rucLimpio,
                    RazonSocial = model.RazonSocial.Trim().ToUpper(),
                    DireccionFiscal = model.DireccionFiscal.Trim(),
                    Departamento = string.IsNullOrWhiteSpace(model.Departamento) ? null : model.Departamento.Trim(),
                    Provincia = string.IsNullOrWhiteSpace(model.Provincia) ? null : model.Provincia.Trim(),
                    Distrito = string.IsNullOrWhiteSpace(model.Distrito) ? null : model.Distrito.Trim(),
                    Ubigeo = string.IsNullOrWhiteSpace(model.Ubigeo) ? null : model.Ubigeo.Trim(),
                    EstadoSunat = string.IsNullOrWhiteSpace(model.EstadoSunat) ? "ACTIVO" : model.EstadoSunat.Trim().ToUpper(),
                    CondicionSunat = string.IsNullOrWhiteSpace(model.CondicionSunat) ? "HABIDO" : model.CondicionSunat.Trim().ToUpper(),
                    Telefono = model.Telefono?.Trim() ?? string.Empty,
                    CorreoElectronico = model.CorreoElectronico?.Trim() ?? string.Empty,
                    FechaCreacion = DateTime.UtcNow,
                    Activo = true
                };

                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                // Detección de riesgo tributario al guardar
                if (cliente.CondicionSunat != "HABIDO" || cliente.EstadoSunat != "ACTIVO")
                {
                    TempData["WarningMessage"] = $"Cliente registrado, pero atención: figura en SUNAT como [{cliente.CondicionSunat}] y [{cliente.EstadoSunat}]. La emisión de Facturas Electrónicas no tendrá crédito fiscal.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Cliente '{cliente.RazonSocial}' (RUC: {cliente.Ruc}) registrado y asociado correctamente con Ubigeo {cliente.Ubigeo ?? "N/A"}.";
                }

                return RedirectToAction(nameof(Create));
            }

            ViewBag.ClientesRegistrados = await _context.Clientes
                .OrderByDescending(c => c.Id)
                .Take(10)
                .ToListAsync();

            return View(model);
        }

        // GET: Clientes/ConsultarSunat?ruc=20100047218
        [HttpGet]
        public async Task<IActionResult> ConsultarSunat(string ruc, CancellationToken cancellationToken)
        {
            var resultado = await _sunatService.ConsultarRucAsync(ruc, cancellationToken);

            if (!resultado.Success)
            {
                return Json(new { success = false, message = resultado.Message });
            }

            return Json(new
            {
                success = true,
                ruc = resultado.Ruc,
                razonSocial = resultado.RazonSocial,
                direccionFiscal = resultado.DireccionFiscal,
                departamento = resultado.Departamento,
                provincia = resultado.Provincia,
                distrito = resultado.Distrito,
                ubigeo = resultado.Ubigeo,
                estado = resultado.Estado,
                condicion = resultado.Condicion,
                tipoEmision = resultado.TipoEmision,
                fuente = resultado.Fuente,
                esHabido = resultado.EsHabido,
                esActivo = resultado.EsActivo
            });
        }
    }
}
