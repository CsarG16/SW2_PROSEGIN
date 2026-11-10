using Prosegin.Web.ViewModels.CuentasPersonal;

namespace Prosegin.Web.Services;

public interface ICuentasPersonalService
{
    Task<CuentaPersonalListViewModel> ObtenerAsync(string? busqueda, string? rol, string? estado, int pagina, CancellationToken cancellationToken);
    Task<CuentaPersonalFormViewModel?> ObtenerFormularioAsync(int? id, CancellationToken cancellationToken);
    Task<(bool Exito, string Mensaje)> GuardarAsync(CuentaPersonalFormViewModel model, CancellationToken cancellationToken);
    Task<(bool Exito, string Mensaje)> DesactivarAsync(int id, string? usuarioActual, CancellationToken cancellationToken);
    Task<(bool Exito, string Mensaje, string? NombreUsuario, string? NombreCompleto, string? Rol)> ValidarAccesoAsync(string usuario, string password, CancellationToken cancellationToken);
}
