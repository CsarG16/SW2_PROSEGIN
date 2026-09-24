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

        [HttpGet]
        public async Task<IActionResult> Index(string? termino, CancellationToken cancellationToken)
        {
            var model = new ClienteBusquedaViewModel { Termino = termino?.Trim() ?? string.Empty };
            if (string.IsNullOrWhiteSpace(model.Termino))
            {
                return View(model);
            }

            var esRuc = model.Termino.All(char.IsDigit);
            if (esRuc)
            {
                if (model.Termino.Length != 11 || (!model.Termino.StartsWith("10") && !model.Termino.StartsWith("20")))
                {
                    model.Mensaje = "El RUC debe contener 11 dígitos y comenzar con 10 o 20.";
                    return View(model);
                }

                if (!_sunatService.ValidarFormatoRuc(model.Termino, out var errorRuc))
                {
                    model.Mensaje = errorRuc;
                    return View(model);
                }
            }
            else if (model.Termino.Length < 3)
            {
                model.Mensaje = "Ingrese al menos 3 letras de la razón social.";
                return View(model);
            }

            var clientesQuery = _context.Clientes
                .AsNoTracking()
                .Where(c => c.Activo);

            clientesQuery = esRuc
                ? clientesQuery.Where(c => c.Ruc == model.Termino)
                : clientesQuery.Where(c => EF.Functions.Like(c.RazonSocial, $"%{model.Termino}%"));

            var clientes = await clientesQuery
                .OrderBy(c => c.RazonSocial)
                .Take(50)
                .ToListAsync(cancellationToken);

            var clienteIds = clientes.Select(c => c.Id).ToArray();
            var facturas = clienteIds.Length == 0
                ? new List<Factura>()
                : await _context.Facturas
                    .AsNoTracking()
                    .Where(f => clienteIds.Contains(f.ClienteId) && f.EstadoPago != "Pagado")
                    .ToListAsync(cancellationToken);

            var ahora = DateTime.UtcNow;
            model.Resultados = clientes.Select(cliente =>
            {
                var facturasCliente = facturas.Where(f => f.ClienteId == cliente.Id).ToList();
                var facturasVencidas = facturasCliente.Where(f => f.FechaVencimiento < ahora).ToList();
                var saldoPendiente = facturasCliente.Sum(f => f.Total);
                var saldoVencido = facturasVencidas.Sum(f => f.Total);
                var diasMora = facturasVencidas.Count == 0
                    ? 0
                    : facturasVencidas.Max(f => Math.Max(0, (ahora.Date - f.FechaVencimiento.Date).Days));
                var creditoExcedido = cliente.LimiteCredito > 0 && saldoPendiente > cliente.LimiteCredito;
                var bloqueado = facturasVencidas.Count > 0 || creditoExcedido;

                return new ClienteBusquedaItemViewModel
                {
                    Id = cliente.Id,
                    Ruc = cliente.Ruc,
                    RazonSocial = cliente.RazonSocial,
                    DireccionFiscal = cliente.DireccionFiscal,
                    RepresentanteLegal = cliente.RepresentanteLegal,
                    EstadoSunat = cliente.EstadoSunat,
                    CondicionSunat = cliente.CondicionSunat,
                    LimiteCredito = cliente.LimiteCredito,
                    SaldoVencido = saldoVencido,
                    DiasMora = diasMora,
                    CreditoExcedido = creditoExcedido,
                    ClienteBloqueado = bloqueado,
                    EstadoCredito = bloqueado ? "BLOQUEADO" : "HABILITADO",
                    MensajeBloqueo = facturasVencidas.Count > 0
                        ? $"Cliente bloqueado por mora: factura(s) vencida(s) hasta {diasMora} días."
                        : creditoExcedido
                            ? "Cliente bloqueado por sobregiro de crédito."
                            : null
                };
            }).ToList();

            if (esRuc && model.Resultados.Count == 0)
            {
                model.ConsultaSunat = await _sunatService.ConsultarRucAsync(model.Termino, cancellationToken);
                if (!model.ConsultaSunat.Success)
                {
                    model.Mensaje = model.ConsultaSunat.Message;
                }
            }
            else if (esRuc && model.Resultados.Count > 0)
            {
                model.ConsultaSunat = await _sunatService.ConsultarRucAsync(model.Termino, cancellationToken);
                var clienteLocal = clientes[0];
                if (model.ConsultaSunat.Success
                    && !string.IsNullOrWhiteSpace(model.ConsultaSunat.DireccionFiscal)
                    && ((!string.Equals(clienteLocal.DireccionFiscal.Trim(), model.ConsultaSunat.DireccionFiscal.Trim(), StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(model.ConsultaSunat.RepresentanteLegal)
                            && !string.Equals(clienteLocal.RepresentanteLegal?.Trim(), model.ConsultaSunat.RepresentanteLegal.Trim(), StringComparison.OrdinalIgnoreCase))))
                {
                    model.MensajeActualizacionSunat = "La dirección fiscal o el representante legal cambió según SUNAT. Debe actualizar los datos del cliente antes de generar un documento de venta.";
                }
            }
            else if (model.Resultados.Count == 0)
            {
                model.Mensaje = "No se encontraron clientes para la razón social ingresada.";
            }

            return View(model);
        }

        // GET: Clientes/Create
        public async Task<IActionResult> Create(
            string? ruc,
            string? razonSocial,
            string? direccionFiscal,
            string? estadoSunat,
            string? condicionSunat,
            string? representanteLegal)
        {
            ViewBag.ClientesRegistrados = await _context.Clientes
                .OrderByDescending(c => c.Id)
                .Take(10)
                .ToListAsync();

            return View(new ClienteCreateViewModel
            {
                Ruc = ruc?.Trim() ?? string.Empty,
                RazonSocial = razonSocial?.Trim() ?? string.Empty,
                DireccionFiscal = direccionFiscal?.Trim() ?? string.Empty,
                EstadoSunat = string.IsNullOrWhiteSpace(estadoSunat) ? "ACTIVO" : estadoSunat.Trim().ToUpper(),
                CondicionSunat = string.IsNullOrWhiteSpace(condicionSunat) ? "HABIDO" : condicionSunat.Trim().ToUpper(),
                RepresentanteLegal = representanteLegal?.Trim()
            });
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
                    RepresentanteLegal = string.IsNullOrWhiteSpace(model.RepresentanteLegal) ? null : model.RepresentanteLegal.Trim().ToUpper(),
                    LimiteCredito = model.LimiteCredito,
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
                    representanteLegal = resultado.RepresentanteLegal,
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
