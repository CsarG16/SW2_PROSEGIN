using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.ViewModels.Clientes;

namespace Prosegin.Web.Controllers
{
    public class ClientesController : Controller
    {
        private readonly ProseginDbContext _context;

        public ClientesController(ProseginDbContext context)
        {
            _context = context;
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
            if (ModelState.IsValid)
            {
                var rucLimpio = model.Ruc.Trim();

                // Validación de RUC duplicado en base de datos
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
                    Telefono = model.Telefono?.Trim() ?? string.Empty,
                    CorreoElectronico = model.CorreoElectronico?.Trim() ?? string.Empty,
                    FechaCreacion = DateTime.UtcNow,
                    Activo = true
                };

                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cliente '{cliente.RazonSocial}' con RUC {cliente.Ruc} guardado y asociado correctamente.";
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
        public async Task<IActionResult> ConsultarSunat(string ruc)
        {
            if (string.IsNullOrWhiteSpace(ruc))
            {
                return Json(new { success = false, message = "Debe ingresar un número de RUC." });
            }

            ruc = ruc.Trim();

            if (ruc.Length != 11 || !ruc.All(char.IsDigit))
            {
                return Json(new { success = false, message = "El RUC debe tener exactamente 11 dígitos numéricos." });
            }

            // 1. Intento de consulta en API pública oficial SUNAT con timeout corto (3s)
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = await httpClient.GetAsync($"https://api.apis.net.pe/v1/ruc?numero={ruc}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    var razonSocial = root.TryGetProperty("nombre", out var n) ? n.GetString() ?? "" : "";
                    var direccion = root.TryGetProperty("direccion", out var d) ? d.GetString() ?? "" : "";
                    var estado = root.TryGetProperty("estado", out var e) ? e.GetString() ?? "ACTIVO" : "ACTIVO";
                    var condicion = root.TryGetProperty("condicion", out var c) ? c.GetString() ?? "HABIDO" : "HABIDO";
                    var departamento = root.TryGetProperty("departamento", out var dep) ? dep.GetString() ?? "LIMA" : "LIMA";
                    var provincia = root.TryGetProperty("provincia", out var prov) ? prov.GetString() ?? "LIMA" : "LIMA";
                    var distrito = root.TryGetProperty("distrito", out var dist) ? dist.GetString() ?? "" : "";
                    var ubigeo = root.TryGetProperty("ubigeo", out var u) ? u.GetString() ?? "150101" : "150101";

                    return Json(new
                    {
                        success = true,
                        ruc = ruc,
                        razonSocial = razonSocial,
                        direccionFiscal = direccion,
                        departamento = departamento,
                        provincia = provincia,
                        distrito = distrito,
                        ubigeo = ubigeo,
                        estado = estado,
                        condicion = condicion,
                        tipoEmision = ruc.StartsWith("20") ? "FACTURA ELECTRÓNICA (TIPO 01)" : "BOLETA / FACTURA ELECTRÓNICA",
                        fuente = "Padrón SUNAT Oficial (En Línea)"
                    });
                }
            }
            catch
            {
                // Fallback silencioso si no hay internet o la API externa no responde
            }

            // Si la API de SUNAT no responde o no encuentra el RUC:
            return Json(new { 
                success = false, 
                message = "No se encontró información en el padrón de SUNAT para este RUC. Puede ingresar la Razón Social y Dirección Fiscal manualmente." 
            });
        }
    }
}
