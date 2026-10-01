using Prosegin.Data.Validation;
using Xunit;

namespace Prosegin.Tests;

public class ClienteRucValidacionTests
{
    [Theory]
    [InlineData("20100047218")]
    [InlineData("10100000003")]
    public void ValidarFormatoRuc_ConPrefijoPermitidoYDigitoVerificadorCorrecto_EsValido(string ruc)
    {
        var valido = RucValidator.ValidarFormato(ruc, out var mensaje);

        Assert.True(valido);
        Assert.Null(mensaje);
    }

    [Theory]
    [InlineData("15100000005")]
    [InlineData("17100000008")]
    [InlineData("")]
    [InlineData("2010004721")]
    [InlineData("20100047219")]
    public void ValidarFormatoRuc_ConRucFueraDelCriterio_EsInvalido(string ruc)
    {
        var valido = RucValidator.ValidarFormato(ruc, out var mensaje);

        Assert.False(valido);
        Assert.Equal("Número de RUC inválido", mensaje);
    }
}
