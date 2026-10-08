using Prosegin.Data.Entities;

namespace Prosegin.Web.ViewModels.Clientes
{
    public class ClienteDireccionesViewModel
    {
        public Cliente Cliente { get; set; } = null!;
        public List<PuntoEntrega> DireccionesActivas { get; set; } = new();
        public PuntoEntregaViewModel NuevaDireccion { get; set; } = new();
    }
}
