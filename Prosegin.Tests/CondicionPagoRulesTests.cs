using Prosegin.Data.Validation;
using Xunit;

namespace Prosegin.Tests;

public class CondicionPagoRulesTests
{
    [Theory]
    [InlineData("7 días")]
    [InlineData("15 días")]
    [InlineData("30 días")]
    public void EsPlazoCreditoValido_AceptaPlazosDeCredito(string condicionPago)
    {
        Assert.True(CondicionPagoRules.EsPlazoCreditoValido(condicionPago));
    }

    [Theory]
    [InlineData("Contado")]
    [InlineData("45 días")]
    [InlineData("")]
    [InlineData(null)]
    public void EsPlazoCreditoValido_RechazaContadoYValoresNoPermitidos(string? condicionPago)
    {
        Assert.False(CondicionPagoRules.EsPlazoCreditoValido(condicionPago));
    }
}
