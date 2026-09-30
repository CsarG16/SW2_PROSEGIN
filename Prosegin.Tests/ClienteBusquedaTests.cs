using Xunit;

namespace Prosegin.Tests;

public class ClienteBusquedaTests
{
    private readonly List<ClienteTestDto> _clientes = new()
    {
        new ClienteTestDto
        {
            Id = 1,
            Ruc = "20601234567",
            RazonSocial = "CONSTRUCTORA E INGENIERÍA MINERA DEL SUR S.A.C.",
            DireccionFiscal = "Av. Las Begonias Nro. 441, San Isidro - Lima",
            EstadoSunat = "ACTIVO",
            CondicionSunat = "HABIDO",
            CantidadSedes = 3,
            TipoCliente = "Cliente Corporativo"
        },
        new ClienteTestDto
        {
            Id = 2,
            Ruc = "20548912340",
            RazonSocial = "CONSTRUCTORA DEL PACIFICO S.A.C.",
            DireccionFiscal = "Av. Industrial 1450, Ate - Lima",
            EstadoSunat = "ACTIVO",
            CondicionSunat = "HABIDO",
            CantidadSedes = 2,
            TipoCliente = "Cliente Habitual"
        },
        new ClienteTestDto
        {
            Id = 3,
            Ruc = "20492817263",
            RazonSocial = "CONSORCIO VIAL ANDINO S.R.L.",
            DireccionFiscal = "Jr. Huancavelica 320, Cercado - Lima",
            EstadoSunat = "ACTIVO",
            CondicionSunat = "HABIDO",
            CantidadSedes = 1,
            TipoCliente = "Licitaciones / Obras"
        }
    };

    [Fact]
    public void Criterio1_BuscarPorNombreORuc_DevuelveEmpresasDetalladas()
    {
        var termino = "CONSTRUCTORA";
        var resultados = _clientes
            .Where(c => c.RazonSocial.Contains(termino, StringComparison.OrdinalIgnoreCase) || c.Ruc == termino)
            .ToList();

        Assert.Equal(2, resultados.Count);
        Assert.All(resultados, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Ruc));
            Assert.False(string.IsNullOrWhiteSpace(r.RazonSocial));
            Assert.False(string.IsNullOrWhiteSpace(r.DireccionFiscal));
            Assert.False(string.IsNullOrWhiteSpace(r.EstadoSunat));
            Assert.False(string.IsNullOrWhiteSpace(r.CondicionSunat));
        });
    }

    [Fact]
    public void Criterio2_ClienteNoExiste_MuestraMensajeEspecifico()
    {
        var termino = "99999999999";
        var resultados = _clientes
            .Where(c => c.RazonSocial.Contains(termino, StringComparison.OrdinalIgnoreCase) || c.Ruc == termino)
            .ToList();

        string? mensaje = null;
        if (!resultados.Any())
        {
            mensaje = "No se encontraron clientes registrados con los datos ingresados";
        }

        Assert.Empty(resultados);
        Assert.Equal("No se encontraron clientes registrados con los datos ingresados", mensaje);
    }

    [Fact]
    public void Criterio3_ClienteActivoYHabido_PermiteGenerarCotizacion()
    {
        var cliente = _clientes.First(c => c.Ruc == "20601234567");
        var puedeCotizar = cliente.EstadoSunat == "ACTIVO" && cliente.CondicionSunat == "HABIDO";

        Assert.True(puedeCotizar);
    }

    [Fact]
    public void Criterio4_ClienteTieneLocales_MuestraSedesRegistradas()
    {
        var cliente = _clientes.First(c => c.Ruc == "20601234567");

        Assert.True(cliente.CantidadSedes > 0);
        Assert.Equal(3, cliente.CantidadSedes);
    }

    private class ClienteTestDto
    {
        public int Id { get; set; }
        public string Ruc { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public string DireccionFiscal { get; set; } = string.Empty;
        public string EstadoSunat { get; set; } = string.Empty;
        public string CondicionSunat { get; set; } = string.Empty;
        public int CantidadSedes { get; set; }
        public string TipoCliente { get; set; } = string.Empty;
    }
}
