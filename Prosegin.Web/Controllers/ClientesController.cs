using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Data.Validation;
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
                var clientesRegistrados = await _context.Clientes
                    .AsNoTracking()
                    .Where(c => c.Activo)
                    .OrderBy(c => c.RazonSocial)
                    .Take(50)
                    .ToListAsync(cancellationToken);

                model.Resultados = await MapearClientesConEstadoCrediticioAsync(clientesRegistrados, cancellationToken);
                return View(model);
            }

            var esRuc = ClienteBusquedaRules.EsEntradaSoloNumeros(model.Termino);
            if (esRuc && !_sunatService.ValidarFormatoRuc(model.Termino, out var errorRuc))
            {
                model.Mensaje = errorRuc;
                return View(model);
            }

            var clientesQuery = _context.Clientes
                .AsNoTracking()
                .Where(c => c.Activo);

            if (esRuc)
            {
                clientesQuery = clientesQuery.Where(c => c.Ruc == model.Termino);
            }
            else
            {
                clientesQuery = clientesQuery.Where(c => EF.Functions.Like(c.RazonSocial, $"%{model.Termino}%"));
            }

            var clientes = await clientesQuery
                .OrderBy(c => c.RazonSocial)
                .Take(50)
                .ToListAsync(cancellationToken);

            model.Resultados = await MapearClientesConEstadoCrediticioAsync(clientes, cancellationToken);

            if (model.Resultados.Count == 0)
            {
                // Criterio de Aceptación 2:
                // DADO QUE el cliente consultado no existe en los registros de la empresa, CUANDO selecciono "BUSCAR", ENTONCES se muestra el MSG: "No se encontraron clientes registrados con los datos ingresados".
                model.Mensaje = ClienteBusquedaRules.MensajeSinResultados;

                if (esRuc)
                {
                    try
                    {
                        model.ConsultaSunat = await _sunatService.ConsultarRucAsync(model.Termino, cancellationToken);
                    }
                    catch
                    {
                        // Fallback si la API SUNAT no responde
                    }
                }
            }
            else if (esRuc && model.Resultados.Count > 0)
            {
                try
                {
                    model.ConsultaSunat = await _sunatService.ConsultarRucAsync(model.Termino, cancellationToken);
                    var clienteLocal = clientes[0];
                    var clienteEnVista = model.Resultados.FirstOrDefault(c => c.Id == clienteLocal.Id);
                    if (model.ConsultaSunat.Success)
                    {
                        if (clienteEnVista != null)
                        {
                            clienteEnVista.EstadoSunat = model.ConsultaSunat.Estado ?? "NO VERIFICADO";
                            clienteEnVista.CondicionSunat = model.ConsultaSunat.Condicion ?? "NO VERIFICADO";
                        }

                        var direccionSunat = model.ConsultaSunat.DireccionFiscal?.Trim();
                        if (!string.IsNullOrWhiteSpace(direccionSunat)
                            && !string.Equals(clienteLocal.DireccionFiscal.Trim(), direccionSunat, StringComparison.OrdinalIgnoreCase))
                        {
                            model.MensajeActualizacionSunat = "La dirección fiscal cambió según SUNAT. Debe actualizar los datos del cliente antes de generar un documento de venta.";
                        }
                    }
                    else
                    {
                        if (clienteEnVista != null)
                        {
                            clienteEnVista.EstadoSunat = "NO VERIFICADO";
                            clienteEnVista.CondicionSunat = "NO VERIFICADO";
                        }
                        model.Mensaje = "No se pudo verificar el estado tributario del cliente en SUNAT. La cotización permanecerá deshabilitada hasta validar sus datos.";
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            return View(model);
        }

        private async Task<List<ClienteBusquedaItemViewModel>> MapearClientesConEstadoCrediticioAsync(
            List<Cliente> clientes, 
            CancellationToken cancellationToken)
        {
            var clienteIds = clientes.Select(c => c.Id).ToArray();
            var facturas = clienteIds.Length == 0
                ? new List<Factura>()
                : await _context.Facturas
                    .AsNoTracking()
                    .Where(f => clienteIds.Contains(f.ClienteId) && f.EstadoPago != "Pagado")
                    .ToListAsync(cancellationToken);

            var sedesConteo = clienteIds.Length == 0
                ? new Dictionary<int, int>()
                : await _context.PuntosEntrega
                    .AsNoTracking()
                    .Where(p => clienteIds.Contains(p.ClienteId) && p.Activo)
                    .GroupBy(p => p.ClienteId)
                    .Select(g => new { ClienteId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.ClienteId, g => g.Count, cancellationToken);

            var ahora = DateTime.UtcNow;
            return clientes.Select(cliente =>
            {
                var facturasCliente = facturas.Where(f => f.ClienteId == cliente.Id).ToList();
                var facturasVencidas = facturasCliente.Where(f => f.FechaVencimiento < ahora).ToList();
                var saldoPendiente = facturasCliente.Sum(f => f.Total);
                var saldoVencido = facturasVencidas.Sum(f => f.Total);
                var diasMora = facturasVencidas.Count == 0
                    ? 0
                    : facturasVencidas.Max(f => Math.Max(0, (ahora.Date - f.FechaVencimiento.Date).Days));
                var bloqueado = facturasVencidas.Count > 0;

                var totalSedes = sedesConteo.TryGetValue(cliente.Id, out var count) ? (count > 0 ? count : 1) : 1;

                string tipoCliente = "Cliente Habitual";
                if (cliente.RazonSocial.Contains("MINERA", StringComparison.OrdinalIgnoreCase) || 
                    cliente.RazonSocial.Contains("SUR", StringComparison.OrdinalIgnoreCase) ||
                    cliente.RazonSocial.Contains("CORPORATIVO", StringComparison.OrdinalIgnoreCase))
                {
                    tipoCliente = "Cliente Corporativo";
                }
                else if (cliente.RazonSocial.Contains("VIAL", StringComparison.OrdinalIgnoreCase) || 
                         cliente.RazonSocial.Contains("CONSORCIO", StringComparison.OrdinalIgnoreCase) ||
                         cliente.RazonSocial.Contains("OBRAS", StringComparison.OrdinalIgnoreCase))
                {
                    tipoCliente = "Licitaciones / Obras";
                }
                else if (cliente.RazonSocial.Contains("PACIFICO", StringComparison.OrdinalIgnoreCase))
                {
                    tipoCliente = "Cliente Habitual";
                }

                return new ClienteBusquedaItemViewModel
                {
                    Id = cliente.Id,
                    Ruc = cliente.Ruc,
                    RazonSocial = cliente.RazonSocial,
                    DireccionFiscal = cliente.DireccionFiscal,
                    Ubigeo = cliente.Ubigeo,
                    Departamento = cliente.Departamento,
                    Provincia = cliente.Provincia,
                    Distrito = cliente.Distrito,
                    EstadoSunat = cliente.EstadoSunat,
                    CondicionSunat = cliente.CondicionSunat,
                    SaldoVencido = saldoVencido,
                    DiasMora = diasMora,
                    CreditoExcedido = false,
                    ClienteBloqueado = bloqueado,
                    EstadoCredito = bloqueado ? "BLOQUEADO" : "HABILITADO",
                    MensajeBloqueo = facturasVencidas.Count > 0
                        ? $"Cliente bloqueado por mora: factura(s) vencida(s) hasta {diasMora} días."
                        : null,
                    FechaCreacion = cliente.FechaCreacion,
                    TipoCliente = tipoCliente,
                    CantidadSedes = totalSedes
                };
            }).ToList();
        }

        // GET: Clientes/Create
        [HttpGet]
        public IActionResult Create(
            string? ruc,
            string? razonSocial,
            string? direccionFiscal,
            string? estadoSunat,
            string? condicionSunat,
            int? clienteId,
            string? paso)
        {
            ViewBag.ClienteId = clienteId;
            ViewBag.Paso = paso;
            return View(new ClienteCreateViewModel
            {
                Ruc = ruc?.Trim() ?? string.Empty,
                RazonSocial = razonSocial?.Trim() ?? string.Empty,
                DireccionFiscal = direccionFiscal?.Trim() ?? string.Empty,
                EstadoSunat = string.IsNullOrWhiteSpace(estadoSunat) ? "NO VERIFICADO" : estadoSunat.Trim().ToUpper(),
                CondicionSunat = string.IsNullOrWhiteSpace(condicionSunat) ? "NO VERIFICADO" : condicionSunat.Trim().ToUpper()
            });
        }

        // POST: Clientes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClienteCreateViewModel model, string? accion = null, CancellationToken cancellationToken = default)
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
                var rucExiste = await _context.Clientes.AnyAsync(c => c.Ruc == rucLimpio, cancellationToken);
                if (rucExiste)
                {
                    ModelState.AddModelError("Ruc", "El RUC ingresado ya se encuentra registrado en el padrón de clientes.");
                    return View(model);
                }

                // Nunca confiar en el estado tributario enviado por el navegador: comprobarlo
                // nuevamente en el servidor al guardar. Si SUNAT no responde, permitir el alta
                // manual dejando el cliente explícitamente sin verificar.
                var consultaSunat = await _sunatService.ConsultarRucAsync(rucLimpio, cancellationToken);
                model.EstadoSunat = consultaSunat.Success
                    ? consultaSunat.Estado ?? "NO VERIFICADO"
                    : "NO VERIFICADO";
                model.CondicionSunat = consultaSunat.Success
                    ? consultaSunat.Condicion ?? "NO VERIFICADO"
                    : "NO VERIFICADO";

                var cliente = new Cliente
                {
                    Ruc = rucLimpio,
                    RazonSocial = model.RazonSocial.Trim().ToUpper(),
                    DireccionFiscal = model.DireccionFiscal.Trim(),
                    Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim(),
                    Departamento = string.IsNullOrWhiteSpace(model.Departamento) ? null : model.Departamento.Trim(),
                    Provincia = string.IsNullOrWhiteSpace(model.Provincia) ? null : model.Provincia.Trim(),
                    Distrito = string.IsNullOrWhiteSpace(model.Distrito) ? null : model.Distrito.Trim(),
                    Ubigeo = string.IsNullOrWhiteSpace(model.Ubigeo) ? null : model.Ubigeo.Trim(),
                    EstadoSunat = model.EstadoSunat.Trim().ToUpper(),
                    CondicionSunat = model.CondicionSunat.Trim().ToUpper(),
                    Telefono = model.Telefono?.Trim() ?? string.Empty,
                    CorreoElectronico = model.CorreoElectronico?.Trim() ?? string.Empty,
                    FechaCreacion = DateTime.UtcNow,
                    Activo = true
                };

                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                // Registrar automáticamente la dirección fiscal como la primera sede de despacho habilitada (Predeterminada)
                if (!string.IsNullOrWhiteSpace(cliente.DireccionFiscal))
                {
                    var sedeFiscalInicial = new PuntoEntrega
                    {
                        ClienteId = cliente.Id,
                        TipoSede = "Sede Central / Oficina",
                        NombreAlias = "Sede Fiscal Administrativa",
                        Direccion = cliente.DireccionFiscal.Trim(),
                        Referencia = !string.IsNullOrWhiteSpace(cliente.Referencia) 
                            ? cliente.Referencia 
                            : "Domicilio fiscal registrado en SUNAT",
                        Departamento = cliente.Departamento ?? "Lima",
                        Provincia = cliente.Provincia ?? "Lima",
                        Distrito = cliente.Distrito ?? "Lima",
                        Ubigeo = cliente.Ubigeo ?? "150101",
                        ContactoRecepcion = "Recepción Administrativa",
                        TelefonoMovil = cliente.Telefono,
                        HorarioRecepcion = "Lun - Vie · 09:00 - 18:00",
                        RestriccionesAcceso = "Recepción de muestras y EPP menor",
                        EsPredeterminada = true,
                        Activo = true
                    };
                    _context.PuntosEntrega.Add(sedeFiscalInicial);
                    await _context.SaveChangesAsync();
                }

                // Detección de riesgo tributario al guardar
                var continuarConDirecciones = !string.IsNullOrWhiteSpace(accion)
                    && accion.Contains("direcciones", StringComparison.OrdinalIgnoreCase);
                if (continuarConDirecciones)
                {
                    TempData["ModalSuccess"] = "Cliente registrado con éxito";
                }
                else
                {
                    TempData["SuccessMessage"] = "Cliente registrado con éxito";
                }

                if (cliente.CondicionSunat != "HABIDO" || cliente.EstadoSunat != "ACTIVO")
                {
                    var mensajeTributario = consultaSunat.Success
                        ? $"Atención: el cliente figura en SUNAT como [{cliente.CondicionSunat}] y [{cliente.EstadoSunat}]. No podrá generar cotizaciones mientras no figure ACTIVO y HABIDO."
                        : "No se pudo verificar el estado tributario en SUNAT. El cliente quedó como NO VERIFICADO y no podrá generar cotizaciones hasta actualizar sus datos.";
                    if (continuarConDirecciones)
                    {
                        TempData["ModalWarning"] = mensajeTributario;
                    }
                    else
                    {
                        TempData["WarningMessage"] = mensajeTributario;
                    }
                }

                // HU 1.3: Conexión para continuar a registrar o gestionar direcciones de entrega
                if (continuarConDirecciones)
                {
                    return RedirectToAction(nameof(Direcciones), new { clienteId = cliente.Id });
                }

                return RedirectToAction(nameof(Create));
            }

            return View(model);
        }

        // GET: Clientes/ConsultarSunat?ruc=20100047218
        [HttpGet]
        public async Task<IActionResult> ConsultarSunat(string ruc, CancellationToken cancellationToken)
        {
            var rucLimpio = ruc?.Trim() ?? string.Empty;

            // HU 1.1: Si el RUC ya se encuentra registrado previamente en la cartera/padrón corporativo, interrumpir la operación
            var yaRegistrado = await _context.Clientes.AnyAsync(c => c.Ruc == rucLimpio, cancellationToken);
            if (yaRegistrado)
            {
                return Json(new
                {
                    success = false,
                    yaRegistrado = true,
                    message = "El RUC ingresado ya se encuentra registrado en el padrón de clientes."
                });
            }

            var resultado = await _sunatService.ConsultarRucAsync(rucLimpio, cancellationToken);

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

        // GET: Clientes/Direcciones?clienteId=1
        [HttpGet]
        public async Task<IActionResult> Direcciones(int? clienteId, CancellationToken cancellationToken)
        {
            Cliente? cliente = null;

            if (clienteId.HasValue)
            {
                cliente = await _context.Clientes
                    .Include(c => c.PuntosEntrega)
                    .FirstOrDefaultAsync(c => c.Id == clienteId.Value, cancellationToken);
            }

            if (cliente == null)
            {
                // Priorizar cliente de referencia (Constructora del Pacífico) o el más reciente
                cliente = await _context.Clientes
                    .Include(c => c.PuntosEntrega)
                    .FirstOrDefaultAsync(c => c.Ruc == "20548912340", cancellationToken)
                    ?? await _context.Clientes
                        .Include(c => c.PuntosEntrega)
                        .OrderByDescending(c => c.Id)
                        .FirstOrDefaultAsync(cancellationToken);
            }

            if (cliente == null)
            {
                TempData["WarningMessage"] = "Debe registrar un cliente primero para gestionar sus direcciones de entrega.";
                return RedirectToAction(nameof(Create));
            }

            var direccionesActivas = await _context.PuntosEntrega
                .Where(p => p.ClienteId == cliente.Id && p.Activo)
                .OrderByDescending(p => p.EsPredeterminada)
                .ThenBy(p => p.Id)
                .ToListAsync(cancellationToken);

            // Si el cliente no tiene direcciones de entrega registradas pero tiene Dirección Fiscal,
            // se auto-guarda y habilita automáticamente como su primera sede autorizada (Predeterminada)
            if (!direccionesActivas.Any() && !string.IsNullOrWhiteSpace(cliente.DireccionFiscal))
            {
                var sedeFiscalInicial = new PuntoEntrega
                {
                    ClienteId = cliente.Id,
                    TipoSede = "Sede Central / Oficina",
                    NombreAlias = "Sede Fiscal Administrativa",
                    Direccion = cliente.DireccionFiscal.Trim(),
                    Referencia = !string.IsNullOrWhiteSpace(cliente.Referencia)
                        ? cliente.Referencia
                        : "Domicilio fiscal registrado en SUNAT",
                    Departamento = !string.IsNullOrWhiteSpace(cliente.Departamento) ? cliente.Departamento : "Lima",
                    Provincia = !string.IsNullOrWhiteSpace(cliente.Provincia) ? cliente.Provincia : "Lima",
                    Distrito = !string.IsNullOrWhiteSpace(cliente.Distrito) ? cliente.Distrito : "Lima",
                    Ubigeo = !string.IsNullOrWhiteSpace(cliente.Ubigeo) ? cliente.Ubigeo : "150101",
                    ContactoRecepcion = "Recepción Administrativa",
                    TelefonoMovil = !string.IsNullOrWhiteSpace(cliente.Telefono) ? cliente.Telefono : string.Empty,
                    HorarioRecepcion = "Lun - Vie · 09:00 - 18:00",
                    RestriccionesAcceso = "Recepción de muestras y EPP menor",
                    EsPredeterminada = true,
                    Activo = true
                };

                _context.PuntosEntrega.Add(sedeFiscalInicial);
                await _context.SaveChangesAsync(cancellationToken);

                direccionesActivas.Add(sedeFiscalInicial);
            }

            var viewModel = new ClienteDireccionesViewModel
            {
                Cliente = cliente,
                DireccionesActivas = direccionesActivas,
                NuevaDireccion = new PuntoEntregaViewModel
                {
                    ClienteId = cliente.Id,
                    Departamento = cliente.Departamento ?? "Lima",
                    Provincia = cliente.Provincia ?? "Lima",
                    Distrito = cliente.Distrito ?? string.Empty,
                    Ubigeo = cliente.Ubigeo ?? string.Empty
                }
            };

            return View(viewModel);
        }

        // POST: Clientes/GuardarDireccion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarDireccion([FromBody] PuntoEntregaViewModel model, CancellationToken cancellationToken)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.TipoSede) ||
                string.IsNullOrWhiteSpace(model.NombreAlias) ||
                string.IsNullOrWhiteSpace(model.Direccion) ||
                string.IsNullOrWhiteSpace(model.Departamento) ||
                string.IsNullOrWhiteSpace(model.Provincia) ||
                string.IsNullOrWhiteSpace(model.Distrito) ||
                string.IsNullOrWhiteSpace(model.Ubigeo))
            {
                return Json(new
                {
                    success = false,
                    message = "Debe completar todos los campos obligatorios marcados con asterisco (*)."
                });
            }

            var clienteExiste = await _context.Clientes.AnyAsync(c => c.Id == model.ClienteId, cancellationToken);
            if (!clienteExiste)
            {
                return Json(new { success = false, message = "Cliente no encontrado en el sistema." });
            }

            // Si se marca como predeterminada, desmarcar las anteriores
            if (model.EsPredeterminada)
            {
                var sedesPrevias = await _context.PuntosEntrega
                    .Where(p => p.ClienteId == model.ClienteId && p.EsPredeterminada)
                    .ToListAsync(cancellationToken);

                foreach (var previa in sedesPrevias)
                {
                    previa.EsPredeterminada = false;
                }
            }
            else
            {
                // Si no tiene ninguna otra sede activa previa, marcar automáticamente como predeterminada
                var tieneOtrasSedes = await _context.PuntosEntrega
                    .AnyAsync(p => p.ClienteId == model.ClienteId && p.Activo && (model.Id == 0 || p.Id != model.Id), cancellationToken);

                if (!tieneOtrasSedes)
                {
                    model.EsPredeterminada = true;
                }
            }

            PuntoEntrega? punto;
            if (model.Id > 0)
            {
                punto = await _context.PuntosEntrega.FirstOrDefaultAsync(p => p.Id == model.Id && p.ClienteId == model.ClienteId, cancellationToken);
                if (punto == null)
                {
                    return Json(new { success = false, message = "No se encontró la sede que desea modificar." });
                }

                punto.TipoSede = model.TipoSede.Trim();
                punto.NombreAlias = model.NombreAlias.Trim();
                punto.Direccion = model.Direccion.Trim();
                punto.Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim();
                punto.Departamento = model.Departamento.Trim();
                punto.Provincia = model.Provincia.Trim();
                punto.Distrito = model.Distrito.Trim();
                punto.Ubigeo = model.Ubigeo.Trim();
                punto.ContactoRecepcion = string.IsNullOrWhiteSpace(model.ContactoRecepcion) ? null : model.ContactoRecepcion.Trim();
                punto.TelefonoMovil = string.IsNullOrWhiteSpace(model.TelefonoMovil) ? null : model.TelefonoMovil.Trim();
                punto.HorarioRecepcion = string.IsNullOrWhiteSpace(model.HorarioRecepcion) ? null : model.HorarioRecepcion.Trim();
                punto.RestriccionesAcceso = string.IsNullOrWhiteSpace(model.RestriccionesAcceso) ? null : model.RestriccionesAcceso.Trim();
                punto.EsPredeterminada = model.EsPredeterminada;
                punto.Activo = true;
            }
            else
            {
                punto = new PuntoEntrega
                {
                    ClienteId = model.ClienteId,
                    TipoSede = model.TipoSede.Trim(),
                    NombreAlias = model.NombreAlias.Trim(),
                    Direccion = model.Direccion.Trim(),
                    Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim(),
                    Departamento = model.Departamento.Trim(),
                    Provincia = model.Provincia.Trim(),
                    Distrito = model.Distrito.Trim(),
                    Ubigeo = model.Ubigeo.Trim(),
                    ContactoRecepcion = string.IsNullOrWhiteSpace(model.ContactoRecepcion) ? null : model.ContactoRecepcion.Trim(),
                    TelefonoMovil = string.IsNullOrWhiteSpace(model.TelefonoMovil) ? null : model.TelefonoMovil.Trim(),
                    HorarioRecepcion = string.IsNullOrWhiteSpace(model.HorarioRecepcion) ? null : model.HorarioRecepcion.Trim(),
                    RestriccionesAcceso = string.IsNullOrWhiteSpace(model.RestriccionesAcceso) ? null : model.RestriccionesAcceso.Trim(),
                    EsPredeterminada = model.EsPredeterminada,
                    Activo = true
                };
                _context.PuntosEntrega.Add(punto);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var totalActivas = await _context.PuntosEntrega
                .CountAsync(p => p.ClienteId == model.ClienteId && p.Activo, cancellationToken);

            return Json(new
            {
                success = true,
                message = model.Id > 0 ? "Sede de entrega actualizada exitosamente." : "Sede de entrega registrada exitosamente.",
                totalActivas,
                direccion = new
                {
                    id = punto.Id,
                    clienteId = punto.ClienteId,
                    tipoSede = punto.TipoSede,
                    nombreAlias = punto.NombreAlias,
                    direccion = punto.Direccion,
                    referencia = punto.Referencia,
                    departamento = punto.Departamento,
                    provincia = punto.Provincia,
                    distrito = punto.Distrito,
                    ubigeo = punto.Ubigeo,
                    contactoRecepcion = punto.ContactoRecepcion,
                    telefonoMovil = punto.TelefonoMovil,
                    horarioRecepcion = punto.HorarioRecepcion,
                    restriccionesAcceso = punto.RestriccionesAcceso,
                    esPredeterminada = punto.EsPredeterminada,
                    activo = punto.Activo
                }
            });
        }

        // GET: Clientes/ObtenerDireccion?id=1
        [HttpGet]
        public async Task<IActionResult> ObtenerDireccion(int id, CancellationToken cancellationToken)
        {
            var punto = await _context.PuntosEntrega
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (punto == null)
            {
                return Json(new { success = false, message = "Sede de entrega no encontrada." });
            }

            return Json(new
            {
                success = true,
                direccion = new
                {
                    id = punto.Id,
                    clienteId = punto.ClienteId,
                    tipoSede = punto.TipoSede,
                    nombreAlias = punto.NombreAlias,
                    direccion = punto.Direccion,
                    referencia = punto.Referencia,
                    departamento = punto.Departamento,
                    provincia = punto.Provincia,
                    distrito = punto.Distrito,
                    ubigeo = punto.Ubigeo,
                    contactoRecepcion = punto.ContactoRecepcion,
                    telefonoMovil = punto.TelefonoMovil,
                    horarioRecepcion = punto.HorarioRecepcion,
                    restriccionesAcceso = punto.RestriccionesAcceso,
                    esPredeterminada = punto.EsPredeterminada,
                    activo = punto.Activo
                }
            });
        }

        // POST: Clientes/DesactivarDireccion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesactivarDireccion([FromBody] DesactivarDireccionRequest request, CancellationToken cancellationToken)
        {
            if (request == null || request.Id <= 0)
            {
                return Json(new { success = false, message = "Identificador de sede inválido." });
            }

            var punto = await _context.PuntosEntrega
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (punto == null)
            {
                return Json(new { success = false, message = "Sede de entrega no encontrada." });
            }

            if (punto.EsPredeterminada)
            {
                return Json(new
                {
                    success = false,
                    message = "No se puede desactivar la dirección predeterminada. Debe designar otra sede como predeterminada antes de desactivar esta."
                });
            }

            punto.Activo = false;
            await _context.SaveChangesAsync(cancellationToken);

            var totalActivas = await _context.PuntosEntrega
                .CountAsync(p => p.ClienteId == punto.ClienteId && p.Activo, cancellationToken);

            return Json(new
            {
                success = true,
                message = $"La sede '{punto.NombreAlias}' ha sido desactivada.",
                totalActivas
            });
        }
    }

    public class DesactivarDireccionRequest
    {
        public int Id { get; set; }
    }
}
// prosegin web ok referencia
