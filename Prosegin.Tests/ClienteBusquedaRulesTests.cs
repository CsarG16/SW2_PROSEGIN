using Prosegin.Data.Validation;
using Xunit;

namespace Prosegin.Tests;

public class ClienteBusquedaRulesTests
{
    [Theory]
    [InlineData("20100047218", true)]
    [InlineData("2010004721", true)]
    [InlineData("201000472188", true)]
    [InlineData("CONSTRUCTORA 20 S.A.C.", false)]
    [InlineData("", false)]
    public void EsEntradaSoloNumeros_ClasificaLaBusqueda(string termino, bool esperado)
    {
        Assert.Equal(esperado, ClienteBusquedaRules.EsEntradaSoloNumeros(termino));
    }

    [Theory]
    [InlineData("ACTIVO", "HABIDO", true)]
    [InlineData("BAJA", "HABIDO", false)]
    [InlineData("ACTIVO", "NO HABIDO", false)]
    [InlineData("NO VERIFICADO", "NO VERIFICADO", false)]
    public void PuedeAbrirCotizacion_RequiereEstadoActivoYCondicionHabido(
        string estado,
        string condicion,
        bool esperado)
    {
        Assert.Equal(esperado, ClienteBusquedaRules.PuedeAbrirCotizacion(estado, condicion));
    }

    [Fact]
    public void MensajeSinResultados_CoincideConElCriterioDeAceptacion()
    {
        Assert.Equal(
            "No se encontraron clientes registrados con los datos ingresados",
            ClienteBusquedaRules.MensajeSinResultados);
    }
}
