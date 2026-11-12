using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Data.Validation;
using Prosegin.Web.Services;
using Prosegin.Web.Services.Nubefact;
using Prosegin.Web.ViewModels.Clientes;

namespace Prosegin.Web.Controllers
{
    public class ClientesController : Controller
    {
        private const int TamanoPaginaHistorial = 10;

        private readonly ProseginDbContext _context;
        private readonly ISunatService _sunatService;
        private readonly IWebHostEnvironment _env;

        public ClientesController(
            ProseginDbContext context,
            ISunatService sunatService,
            IWebHostEnvironment env)
        {
            _context = context;
            _sunatService = sunatService;
            _env = env;
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

            var esEntradaNumerica = ClienteBusquedaRules.EsEntradaSoloNumeros(model.Termino);
            var esRucCompleto = esEntradaNumerica && model.Termino.Length == 11;

            if (esEntradaNumerica && model.Termino.Length >= 11 && !_sunatService.ValidarFormatoRuc(model.Termino, out var errorRuc))
            {
                model.Mensaje = errorRuc;
                return View(model);
            }

            var clientesQuery = _context.Clientes
                .AsNoTracking()
                .Where(c => c.Activo);

            if (esRucCompleto)
            {
                clientesQuery = clientesQuery.Where(c => c.Ruc == model.Termino);
            }
            else if (esEntradaNumerica)
            {
                clientesQuery = clientesQuery.Where(c => c.Ruc.Contains(model.Termino));
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

                if (esRucCompleto)
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
            else if (esRucCompleto && model.Resultados.Count > 0)
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
            ViewBag.FormAction = "Create";
            ViewBag.FormTitle = "Registrar cliente";
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
            ValidarContactos(model);
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
                    TempData["ToastError"] = "El RUC ingresado ya se encuentra registrado en el padrón de clientes.";
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

                cliente.Contactos = model.Contactos.Select(contacto => new ContactoCliente
                {
                    Nombre = contacto.Nombre.Trim(),
                    Cargo = contacto.Cargo.Trim(),
                    Telefono = contacto.Telefono.Trim(),
                    Correo = contacto.Correo.Trim(),
                    EsPrincipal = contacto.EsPrincipal
                }).ToList();

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
                    TempData["ModalSuccess"] = "Cliente guardado exitosamente";
                }
                else
                {
                    TempData["SuccessMessage"] = "Cliente guardado exitosamente";
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

            TempData["ToastError"] = ObtenerMensajeValidacionContacto();
            ViewBag.FormAction = "Create";
            ViewBag.FormTitle = "Registrar cliente";
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var cliente = await _context.Clientes
                .Include(c => c.Contactos)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (cliente == null)
                return NotFound();

            ViewBag.FormAction = "Edit";
            ViewBag.FormTitle = "Editar cliente";
            return View("Create", MapearClienteViewModel(cliente));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ClienteCreateViewModel model, string? accion = null, CancellationToken cancellationToken = default)
        {
            ValidarContactos(model);
            if (ModelState.IsValid)
            {
                var cliente = await _context.Clientes
                    .Include(c => c.Contactos)
                    .FirstOrDefaultAsync(c => c.Id == model.ClienteId, cancellationToken);
                if (cliente == null)
                    return NotFound();

                cliente.RazonSocial = model.RazonSocial.Trim().ToUpperInvariant();
                cliente.DireccionFiscal = model.DireccionFiscal.Trim();
                cliente.Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim();
                cliente.Departamento = string.IsNullOrWhiteSpace(model.Departamento) ? null : model.Departamento.Trim();
                cliente.Provincia = string.IsNullOrWhiteSpace(model.Provincia) ? null : model.Provincia.Trim();
                cliente.Distrito = string.IsNullOrWhiteSpace(model.Distrito) ? null : model.Distrito.Trim();
                cliente.Ubigeo = string.IsNullOrWhiteSpace(model.Ubigeo) ? null : model.Ubigeo.Trim();
                cliente.Telefono = model.Telefono?.Trim() ?? string.Empty;
                cliente.CorreoElectronico = model.CorreoElectronico?.Trim() ?? string.Empty;

                _context.ContactosCliente.RemoveRange(cliente.Contactos);
                cliente.Contactos = model.Contactos.Select(contacto => new ContactoCliente
                {
                    ClienteId = cliente.Id,
                    Nombre = contacto.Nombre.Trim(),
                    Cargo = contacto.Cargo.Trim(),
                    Telefono = contacto.Telefono.Trim(),
                    Correo = contacto.Correo.Trim(),
                    EsPrincipal = contacto.EsPrincipal
                }).ToList();
                await _context.SaveChangesAsync(cancellationToken);
                TempData["SuccessMessage"] = "Cliente guardado exitosamente";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.FormAction = "Edit";
            ViewBag.FormTitle = "Editar cliente";
            TempData["ToastError"] = ObtenerMensajeValidacionContacto();
            return View("Create", model);
        }

        private static ClienteCreateViewModel MapearClienteViewModel(Cliente cliente) =>
            new()
            {
                ClienteId = cliente.Id,
                Ruc = cliente.Ruc,
                RazonSocial = cliente.RazonSocial,
                DireccionFiscal = cliente.DireccionFiscal,
                Referencia = cliente.Referencia,
                Telefono = cliente.Telefono,
                CorreoElectronico = cliente.CorreoElectronico,
                Departamento = cliente.Departamento,
                Provincia = cliente.Provincia,
                Distrito = cliente.Distrito,
                Ubigeo = cliente.Ubigeo,
                EstadoSunat = cliente.EstadoSunat,
                CondicionSunat = cliente.CondicionSunat,
                Contactos = cliente.Contactos.OrderByDescending(c => c.EsPrincipal).ThenBy(c => c.Id)
                    .Select(c => new ContactoClienteViewModel
                    {
                        Nombre = c.Nombre,
                        Cargo = c.Cargo,
                        Telefono = c.Telefono,
                        Correo = c.Correo,
                        EsPrincipal = c.EsPrincipal
                    }).ToList()
            };

        private void ValidarContactos(ClienteCreateViewModel model)
        {
            model.Contactos ??= new List<ContactoClienteViewModel>();
            if (model.Contactos.Count > 3)
                ModelState.AddModelError("Contactos", "Solo se permite un máximo de 3 contactos por cliente");

            var principales = model.Contactos.Count(c => c.EsPrincipal);
            if (principales > 1)
                ModelState.AddModelError("Contactos", "Solo puede existir un contacto principal.");
            else if (model.Contactos.Count > 0 && principales == 0)
                ModelState.AddModelError("Contactos", "Seleccione un contacto principal");

            for (var i = 0; i < model.Contactos.Count; i++)
            {
                var contacto = model.Contactos[i];
                if (string.IsNullOrWhiteSpace(contacto.Nombre)
                    || string.IsNullOrWhiteSpace(contacto.Cargo)
                    || string.IsNullOrWhiteSpace(contacto.Telefono)
                    || string.IsNullOrWhiteSpace(contacto.Correo))
                {
                    ModelState.AddModelError($"Contactos[{i}]", "Complete los campos obligatorios del contacto");
                }

                if (!string.IsNullOrWhiteSpace(contacto.Telefono)
                    && contacto.Telefono.Any(char.IsLetter))
                {
                    ModelState.AddModelError($"Contactos[{i}].Telefono", "El teléfono debe contener solo números");
                }
                else if (!string.IsNullOrWhiteSpace(contacto.Telefono)
                    && !System.Text.RegularExpressions.Regex.IsMatch(contacto.Telefono, @"^9[0-9]{8}$"))
                {
                    ModelState.AddModelError($"Contactos[{i}].Telefono", "El teléfono debe ser un celular de 9 dígitos");
                }

                if (!string.IsNullOrWhiteSpace(contacto.Correo)
                    && !System.Text.RegularExpressions.Regex.IsMatch(
                        contacto.Correo,
                        @"^[A-Za-z0-9]+(?:[._-][A-Za-z0-9]+)*@[A-Za-z0-9]+(?:[.-][A-Za-z0-9]+)*\.[A-Za-z]{2,}$"))
                {
                    ModelState.AddModelError($"Contactos[{i}].Correo", "Ingrese un correo electrónico válido");
                }
            }
        }

        private string ObtenerMensajeValidacionContacto()
        {
            var errores = ModelState
                .SelectMany(par => par.Value?.Errors.Select(error => error.ErrorMessage)
                    ?? Enumerable.Empty<string>())
                .Where(mensaje => !string.IsNullOrWhiteSpace(mensaje))
                .ToList();

            return errores.FirstOrDefault(mensaje => mensaje.Contains("Solo se permite", StringComparison.OrdinalIgnoreCase))
                   ?? errores.FirstOrDefault(mensaje => mensaje.Contains("Seleccione un contacto principal", StringComparison.OrdinalIgnoreCase))
                   ?? errores.FirstOrDefault(mensaje => mensaje.Contains("Complete los campos obligatorios del contacto", StringComparison.OrdinalIgnoreCase))
                   ?? errores.FirstOrDefault(mensaje => mensaje.Contains("debe contener solo números", StringComparison.OrdinalIgnoreCase))
                   ?? errores.FirstOrDefault(mensaje => mensaje.Contains("debe ser un celular", StringComparison.OrdinalIgnoreCase))
                   ?? errores.FirstOrDefault(mensaje => mensaje.Contains("correo electrónico válido", StringComparison.OrdinalIgnoreCase))
                   ?? "Revisa los campos obligatorios antes de guardar.";
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
                string.IsNullOrWhiteSpace(model.Referencia) ||
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

            if (!System.Text.RegularExpressions.Regex.IsMatch(model.Ubigeo.Trim(), @"^\d{6}$"))
            {
                return Json(new
                {
                    success = false,
                    message = "El código de Ubigeo debe tener exactamente 6 dígitos."
                });
            }

            if (model.Referencia!.Trim().Length > 200)
            {
                return Json(new
                {
                    success = false,
                    message = "La referencia no puede superar los 200 caracteres."
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

        // GET: Clientes/Historial/5
        [HttpGet]
        public async Task<IActionResult> Historial(
            int id,
            bool soloCompras = false,
            int pagina = 1,
            CancellationToken cancellationToken = default)
        {
            var cliente = await _context.Clientes
                .AsNoTracking()
                .Include(c => c.PuntosEntrega)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (cliente == null)
            {
                TempData["ToastWarning"] = "Cliente no encontrado.";
                return RedirectToAction(nameof(Index));
            }

            var condicionEntregaCliente = ResolverCondicionEntregaCliente(cliente);
            var registros = new List<ClienteHistorialDocumentoViewModel>();

            if (!soloCompras)
            {
                var cotizaciones = await _context.Cotizaciones
                    .AsNoTracking()
                    .Include(c => c.Detalles)
                        .ThenInclude(d => d.Producto)
                    .Where(c => c.ClienteId == id && c.Estado != "Borrador")
                    .ToListAsync(cancellationToken);

                foreach (var cot in cotizaciones)
                {
                    var (estadoTexto, estadoCss) = MapearEstadoCotizacion(cot.Estado);
                    var tasaIgv = cot.Subtotal > 0m ? cot.Igv / cot.Subtotal : 0.18m;

                    registros.Add(new ClienteHistorialDocumentoViewModel
                    {
                        Id = cot.Id,
                        Tipo = "Cotización",
                        NumeroDocumento = cot.Correlativo,
                        FechaEmision = cot.FechaEmision,
                        Moneda = "PEN",
                        Subtotal = cot.Subtotal,
                        Igv = cot.Igv,
                        Total = cot.Total,
                        Estado = estadoTexto,
                        EstadoCss = estadoCss,
                        PdfDisponible = cot.Detalles.Count > 0,
                        CondicionesPago = cot.CondicionPago,
                        CondicionesEntrega = condicionEntregaCliente,
                        NumeroOrdenCompraCliente = cot.NumeroOrdenCompraCliente,
                        Items = cot.Detalles
                            .OrderBy(d => d.Id)
                            .Select(d => new ClienteHistorialItemViewModel
                            {
                                Sku = d.Producto?.Sku ?? "—",
                                Producto = string.IsNullOrWhiteSpace(d.NombreProducto)
                                    ? (d.Producto?.Nombre ?? "Producto sin nombre")
                                    : d.NombreProducto,
                                UnidadMedida = d.Producto?.UnidadMedida ?? "UND",
                                Cantidad = d.Cantidad,
                                PrecioUnitario = d.PrecioVentaCalculado,
                                Subtotal = d.Subtotal,
                                Total = decimal.Round(d.Subtotal * (1m + tasaIgv), 2, MidpointRounding.AwayFromZero)
                            })
                            .ToList()
                    });
                }
            }

            var ventasFormalizadas = await _context.OrdenesVenta
                .AsNoTracking()
                .Include(ov => ov.Cotizacion)
                    .ThenInclude(c => c.Detalles)
                        .ThenInclude(d => d.Producto)
                .Include(ov => ov.Facturas)
                    .ThenInclude(f => f.Detalles)
                .Include(ov => ov.GuiasRemision)
                .Where(ov => ov.Cotizacion.ClienteId == id
                             && ov.EstadoLogistico != "Cancelado"
                             && (ov.EstadoLogistico == "Entregado"
                                 || ov.Facturas.Any(f => f.EstadoEmision != NubefactService.EstadoRechazado)))
                .ToListAsync(cancellationToken);

            foreach (var venta in ventasFormalizadas)
            {
                var factura = venta.Facturas
                    .Where(f => f.EstadoEmision != NubefactService.EstadoRechazado)
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefault();

                var guia = venta.GuiasRemision
                    .Where(g => g.EstadoEmision != NubefactService.EstadoRechazado)
                    .OrderByDescending(g => g.Id)
                    .FirstOrDefault();

                var numeroDocumento = !string.IsNullOrWhiteSpace(factura?.NumeroFactura)
                    ? factura.NumeroFactura
                    : !string.IsNullOrWhiteSpace(venta.Cotizacion.NumeroOrdenCompraCliente)
                        ? venta.Cotizacion.NumeroOrdenCompraCliente
                        : $"OV-{venta.Id:D6}";

                var subtotal = factura != null && factura.Detalles.Count > 0
                    ? factura.Detalles.Sum(d => d.Subtotal)
                    : venta.Cotizacion.Subtotal;
                var igv = factura != null && factura.Detalles.Count > 0
                    ? factura.Detalles.Sum(d => d.Igv)
                    : venta.Cotizacion.Igv;
                var total = factura?.Total ?? venta.Cotizacion.Total;
                var tasaIgv = subtotal > 0m ? igv / subtotal : 0.18m;

                var items = factura != null && factura.Detalles.Count > 0
                    ? factura.Detalles
                        .OrderBy(d => d.Id)
                        .Select(d => new ClienteHistorialItemViewModel
                        {
                            Sku = d.Codigo,
                            Producto = d.Descripcion,
                            UnidadMedida = d.UnidadMedida,
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.ValorUnitario,
                            Subtotal = d.Subtotal,
                            Total = d.Total
                        })
                        .ToList()
                    : venta.Cotizacion.Detalles
                        .OrderBy(d => d.Id)
                        .Select(d => new ClienteHistorialItemViewModel
                        {
                            Sku = d.Producto?.Sku ?? "—",
                            Producto = string.IsNullOrWhiteSpace(d.NombreProducto)
                                ? (d.Producto?.Nombre ?? "Producto sin nombre")
                                : d.NombreProducto,
                            UnidadMedida = d.Producto?.UnidadMedida ?? "UND",
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioVentaCalculado,
                            Subtotal = d.Subtotal,
                            Total = decimal.Round(d.Subtotal * (1m + tasaIgv), 2, MidpointRounding.AwayFromZero)
                        })
                        .ToList();

                registros.Add(new ClienteHistorialDocumentoViewModel
                {
                    Id = venta.Id,
                    Tipo = "Compra",
                    NumeroDocumento = numeroDocumento,
                    FechaEmision = factura?.FechaEmision ?? venta.FechaCreacion,
                    Moneda = "PEN",
                    Subtotal = subtotal,
                    Igv = igv,
                    Total = total,
                    Estado = "Aprobada",
                    EstadoCss = "badge-status-success",
                    PdfDisponible = items.Count > 0,
                    CondicionesPago = venta.Cotizacion.CondicionPago,
                    CondicionesEntrega = !string.IsNullOrWhiteSpace(guia?.PuntoLlegadaDireccion)
                        ? guia.PuntoLlegadaDireccion
                        : condicionEntregaCliente,
                    NumeroOrdenCompraCliente = venta.Cotizacion.NumeroOrdenCompraCliente,
                    ReferenciaOrigen = venta.Cotizacion.Correlativo,
                    Items = items
                });
            }

            var ordenados = registros
                .OrderByDescending(r => r.FechaEmision)
                .ThenByDescending(r => r.Id)
                .ToList();

            var totalRegistros = ordenados.Count;
            var totalPaginas = Math.Max(1, (int)Math.Ceiling(totalRegistros / (double)TamanoPaginaHistorial));
            var paginaActual = Math.Clamp(pagina < 1 ? 1 : pagina, 1, totalPaginas);
            var paginados = ordenados
                .Skip((paginaActual - 1) * TamanoPaginaHistorial)
                .Take(TamanoPaginaHistorial)
                .ToList();

            var model = new ClienteHistorialViewModel
            {
                Cliente = cliente,
                SoloCompras = soloCompras,
                Pagina = paginaActual,
                TotalPaginas = totalPaginas,
                TotalRegistros = totalRegistros,
                Registros = paginados,
                Mensaje = paginados.Count == 0
                    ? (soloCompras
                        ? "Esta empresa cliente aún no registra compras formalizadas."
                        : "Esta empresa cliente aún no cuenta con cotizaciones ni compras registradas.")
                    : null
            };

            return View(model);
        }

        // GET: Clientes/DescargarHistorial/5?tipo=Cotizacion&docId=10
        [HttpGet]
        public async Task<IActionResult> DescargarHistorial(
            int id,
            string tipo,
            int docId,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(tipo, "Cotizacion", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipo, "Cotización", StringComparison.OrdinalIgnoreCase))
            {
                var cotizacion = await _context.Cotizaciones
                    .AsNoTracking()
                    .Include(c => c.Cliente)
                    .Include(c => c.Detalles)
                        .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(c => c.Id == docId && c.ClienteId == id && c.Estado != "Borrador", cancellationToken);

                if (cotizacion == null || cotizacion.Detalles.Count == 0)
                {
                    TempData["ToastWarning"] = "El documento de cotización solicitado no está disponible para descarga.";
                    return RedirectToAction(nameof(Historial), new { id });
                }

                var pdfBytes = CotizacionPdfGenerator.GenerarConFichas(
                    cotizacion,
                    _env.WebRootPath,
                    productoId => Url.Action("VerFicha", "Catalogo", new { id = productoId }, Request.Scheme));

                return File(pdfBytes, "application/pdf", CotizacionPdfGenerator.NombreArchivoCotizacion(cotizacion));
            }

            if (string.Equals(tipo, "Compra", StringComparison.OrdinalIgnoreCase))
            {
                var venta = await _context.OrdenesVenta
                    .AsNoTracking()
                    .Include(ov => ov.Cotizacion)
                        .ThenInclude(c => c.Cliente)
                    .Include(ov => ov.Cotizacion)
                        .ThenInclude(c => c.Detalles)
                            .ThenInclude(d => d.Producto)
                    .Include(ov => ov.Facturas)
                        .ThenInclude(f => f.Detalles)
                    .FirstOrDefaultAsync(ov => ov.Id == docId
                                               && ov.Cotizacion.ClienteId == id
                                               && ov.EstadoLogistico != "Cancelado"
                                               && (ov.EstadoLogistico == "Entregado"
                                                   || ov.Facturas.Any(f => f.EstadoEmision != NubefactService.EstadoRechazado)),
                        cancellationToken);

                if (venta == null)
                {
                    TempData["ToastWarning"] = "El documento de compra solicitado no está disponible para descarga.";
                    return RedirectToAction(nameof(Historial), new { id });
                }

                var factura = venta.Facturas
                    .Where(f => f.EstadoEmision != NubefactService.EstadoRechazado)
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefault();

                var codigoArchivo = !string.IsNullOrWhiteSpace(factura?.NumeroFactura)
                    ? factura.NumeroFactura
                    : !string.IsNullOrWhiteSpace(venta.Cotizacion.NumeroOrdenCompraCliente)
                        ? venta.Cotizacion.NumeroOrdenCompraCliente
                        : $"OV-{venta.Id:D6}";

                if ((factura?.Detalles.Count ?? 0) == 0 && venta.Cotizacion.Detalles.Count == 0)
                {
                    TempData["ToastWarning"] = "El comprobante no registra productos disponibles para generar el PDF.";
                    return RedirectToAction(nameof(Historial), new { id });
                }

                var localPdfBytes = CompraPdfGenerator.Generate(
                    venta,
                    factura,
                    CotizacionPdfGenerator.ResolverLogoPath(_env.WebRootPath));
                return File(localPdfBytes, "application/pdf",
                    $"{CotizacionPdfGenerator.SanitizeFileName(codigoArchivo)}.pdf");
            }

            TempData["ToastWarning"] = "Tipo de documento no reconocido.";
            return RedirectToAction(nameof(Historial), new { id });
        }

        private static string ResolverCondicionEntregaCliente(Cliente cliente)
        {
            var sedeDefault = cliente.PuntosEntrega
                .Where(p => p.Activo)
                .OrderByDescending(p => p.EsPredeterminada)
                .ThenBy(p => p.Id)
                .FirstOrDefault();

            if (sedeDefault != null)
            {
                var partes = new List<string>();
                if (!string.IsNullOrWhiteSpace(sedeDefault.NombreAlias))
                {
                    partes.Add(sedeDefault.NombreAlias.Trim());
                }
                if (!string.IsNullOrWhiteSpace(sedeDefault.Direccion))
                {
                    partes.Add(sedeDefault.Direccion.Trim());
                }
                if (!string.IsNullOrWhiteSpace(sedeDefault.HorarioRecepcion))
                {
                    partes.Add($"Horario: {sedeDefault.HorarioRecepcion.Trim()}");
                }
                return string.Join(" — ", partes);
            }

            return !string.IsNullOrWhiteSpace(cliente.DireccionFiscal)
                ? cliente.DireccionFiscal
                : "Entrega según coordinación comercial";
        }

        private static (string Texto, string Css) MapearEstadoCotizacion(string estado) => estado switch
        {
            "Aprobada" => ("Aprobada", "badge-status-success"),
            "Enviada" => ("Pendiente", "badge-status-warning"),
            "Rechazada" => ("Rechazada", "badge-status-danger"),
            "Vencida" => ("Vencida", "badge-status-danger"),
            _ => (estado, "badge-status-neutral")
        };
    }

    public class DesactivarDireccionRequest
    {
        public int Id { get; set; }
    }
}
// prosegin web ok referencia
