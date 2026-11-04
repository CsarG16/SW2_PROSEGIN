using Prosegin.Web.Controllers;
using Xunit;

namespace Prosegin.Tests;

public class OrdenesCompraBusquedaTests
{
    [Theory]
    [InlineData("Seguridad & Cía")]
    [InlineData("Seguridad, Cia S.A.C.")]
    [InlineData("Seguridad/Industrial (Peru)")]
    public void BusquedaValida_PermiteCaracteresComunes(string termino)
    {
        Assert.True(OrdenesCompraController.BusquedaValida(termino));
    }

    [Theory]
    [InlineData("Cía", "Cia")]
    [InlineData("PROVEEDOR & CÍA", "proveedor")]
    [InlineData("PROVEEDOR & CÍA", "cia")]
    public void CoincideBusqueda_NoDistingueTildes(string valor, string termino)
    {
        Assert.True(OrdenesCompraController.CoincideBusqueda(valor, termino));
    }

    [Fact]
    public void CoincideBusqueda_ReconoceRucExacto()
    {
        Assert.True(OrdenesCompraController.CoincideBusqueda("20100123330", "20100123330"));
    }

    [Theory]
    [InlineData("@@##%%")]
    [InlineData("")]
    public void BusquedaValida_RechazaValoresInvalidos(string termino)
    {
        Assert.False(OrdenesCompraController.BusquedaValida(termino));
    }
}
