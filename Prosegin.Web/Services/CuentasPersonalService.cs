using Microsoft.EntityFrameworkCore;
using Prosegin.Data;
using Prosegin.Data.Entities;
using Prosegin.Web.ViewModels.CuentasPersonal;

namespace Prosegin.Web.Services;

public sealed class CuentasPersonalService : ICuentasPersonalService
{
    private const int TamanoPagina = 5;
    private static readonly string[] RolesPermitidos = ["Administrador", "Ventas", "Finanzas", "Compras"];
    private readonly ProseginDbContext _context;

    public CuentasPersonalService(ProseginDbContext context)
    {
        _context = context;
    }

    public async Task<CuentaPersonalListViewModel> ObtenerAsync(string? busqueda, string? rol, string? estado, int pagina, CancellationToken cancellationToken)
    {
        var query = _context.CuentasPersonal.AsNoTracking().AsQueryable();
        var filtroRol = string.IsNullOrWhiteSpace(rol) ? "Todos" : rol.Trim();
        var filtroEstado = string.IsNullOrWhiteSpace(estado) ? "Todos" : estado.Trim();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var termino = busqueda.Trim().ToLower();
            query = query.Where(c => c.NombreUsuario.ToLower().Contains(termino)
                || c.NombreCompleto.ToLower().Contains(termino)
                || c.CorreoElectronico.ToLower().Contains(termino));
        }
        if (!filtroRol.Equals("Todos", StringComparison.OrdinalIgnoreCase))
            query = query.Where(c => c.Rol == filtroRol);
        if (filtroEstado.Equals("Activo", StringComparison.OrdinalIgnoreCase))
            query = query.Where(c => c.Activa);
        else if (filtroEstado.Equals("Inactivo", StringComparison.OrdinalIgnoreCase))
            query = query.Where(c => !c.Activa);

        var total = await query.CountAsync(cancellationToken);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)TamanoPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);
        var cuentas = await query.OrderBy(c => c.NombreUsuario)
            .Skip((pagina - 1) * TamanoPagina)
            .Take(TamanoPagina)
            .Select(c => new CuentaPersonalItemViewModel
            {
                Id = c.Id, NombreUsuario = c.NombreUsuario, NombreCompleto = c.NombreCompleto,
                CorreoElectronico = c.CorreoElectronico, Rol = c.Rol, Activa = c.Activa
            }).ToListAsync(cancellationToken);

        return new CuentaPersonalListViewModel
        {
            Busqueda = busqueda, Rol = filtroRol, Estado = filtroEstado, Pagina = pagina,
            TotalPaginas = totalPaginas, TotalRegistros = total, Cuentas = cuentas
        };
    }

    public async Task<CuentaPersonalFormViewModel?> ObtenerFormularioAsync(int? id, CancellationToken cancellationToken)
    {
        if (!id.HasValue)
            return new CuentaPersonalFormViewModel();
        return await _context.CuentasPersonal.AsNoTracking().Where(c => c.Id == id.Value)
            .Select(c => new CuentaPersonalFormViewModel
            {
                Id = c.Id, NombreCompleto = c.NombreCompleto, NombreUsuario = c.NombreUsuario,
                CorreoElectronico = c.CorreoElectronico, Rol = c.Rol, Activa = c.Activa
            }).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Exito, string Mensaje)> GuardarAsync(CuentaPersonalFormViewModel model, CancellationToken cancellationToken)
    {
        if (!RolesPermitidos.Contains(model.Rol, StringComparer.OrdinalIgnoreCase))
            return (false, "Complete los campos obligatorios");
        if (model.Id == 0 && string.IsNullOrWhiteSpace(model.Password))
            return (false, "Complete los campos obligatorios");
        if (!string.IsNullOrWhiteSpace(model.Password) && !CumpleSeguridad(model.Password))
            return (false, "La contraseña no cumple con los requisitos de seguridad");
        if (!string.Equals(model.Password, model.ConfirmarPassword, StringComparison.Ordinal))
            return (false, "Las contraseñas no coinciden");

        var usuario = model.NombreUsuario.Trim();
        var correo = model.CorreoElectronico.Trim().ToLowerInvariant();
        if (await _context.CuentasPersonal.AnyAsync(c => c.NombreUsuario == usuario && c.Id != model.Id, cancellationToken))
            return (false, "El nombre de usuario ya se encuentra registrado");
        if (await _context.CuentasPersonal.AnyAsync(c => c.CorreoElectronico == correo && c.Id != model.Id, cancellationToken))
            return (false, "El correo electrónico ya se encuentra registrado");

        CuentaPersonal cuenta;
        if (model.Id == 0)
        {
            cuenta = new CuentaPersonal { FechaCreacion = DateTime.UtcNow };
            _context.CuentasPersonal.Add(cuenta);
        }
        else
        {
            cuenta = await _context.CuentasPersonal.FirstOrDefaultAsync(c => c.Id == model.Id, cancellationToken)
                ?? throw new InvalidOperationException("La cuenta no existe.");
        }

        cuenta.NombreCompleto = model.NombreCompleto.Trim();
        cuenta.NombreUsuario = usuario;
        cuenta.CorreoElectronico = correo;
        cuenta.Rol = model.Rol;
        cuenta.Activa = model.Activa;
        cuenta.FechaActualizacion = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(model.Password))
            cuenta.PasswordHash = PasswordHashService.Hash(model.Password);
        await _context.SaveChangesAsync(cancellationToken);
        return (true, model.Id == 0 ? "Cuenta registrada correctamente" : "Cuenta actualizada correctamente");
    }

    public async Task<(bool Exito, string Mensaje)> DesactivarAsync(int id, string? usuarioActual, CancellationToken cancellationToken)
    {
        var cuenta = await _context.CuentasPersonal.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cuenta == null)
            return (false, "La cuenta no existe");
        if (!cuenta.Activa)
            return (false, "La cuenta ya se encuentra inactiva");
        if (!string.IsNullOrWhiteSpace(usuarioActual) && cuenta.NombreUsuario.Equals(usuarioActual, StringComparison.OrdinalIgnoreCase))
            return (false, "No puede desactivar su propia cuenta");
        cuenta.Activa = false;
        cuenta.FechaActualizacion = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return (true, "Cuenta desactivada correctamente");
    }

    public async Task<(bool Exito, string Mensaje, string? NombreUsuario, string? NombreCompleto, string? Rol)> ValidarAccesoAsync(string usuario, string password, CancellationToken cancellationToken)
    {
        var usuarioNormalizado = usuario.Trim().ToLower();
        var cuenta = await _context.CuentasPersonal
            .FirstOrDefaultAsync(c => c.NombreUsuario.ToLower() == usuarioNormalizado, cancellationToken);
        if (cuenta == null || !PasswordHashService.Verify(password, cuenta.PasswordHash))
            return (false, "El usuario o la contraseña son incorrectos", null, null, null);
        if (!cuenta.Activa)
            return (false, "La cuenta se encuentra inactiva", null, null, null);
        return (true, string.Empty, cuenta.NombreUsuario, cuenta.NombreCompleto, cuenta.Rol);
    }

    private static bool CumpleSeguridad(string password) =>
        password.Length >= 8
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit)
        && password.Any(ch => !char.IsLetterOrDigit(ch));
}
