using Prosegin.Web.Services;
using Xunit;

namespace Prosegin.Tests;

public class UnidadMedidaSunatMappingTests
{
    [Theory]
    [InlineData("PAR", "PAR")]
    [InlineData("PARES", "PAR")]
    [InlineData("pr", "PAR")]
    [InlineData("CJA", "BX")]
    [InlineData("caja", "BX")]
    [InlineData("CAJAS", "BX")]
    [InlineData("BX", "BX")]
    [InlineData("SET", "SET")]
    [InlineData("juego", "SET")]
    [InlineData("JUEGOS", "SET")]
    [InlineData("KG", "KGM")]
    [InlineData("KGM", "KGM")]
    [InlineData("kilogramo", "KGM")]
    [InlineData("KILOGRAMOS", "KGM")]
    [InlineData("M", "MTR")]
    [InlineData("MTR", "MTR")]
    [InlineData("metro", "MTR")]
    [InlineData("METROS", "MTR")]
    [InlineData("UND", "NIU")]
    [InlineData("UNI", "NIU")]
    [InlineData("unidad", "NIU")]
    [InlineData("UNIDADES", "NIU")]
    [InlineData("PZA", "NIU")]
    [InlineData("pieza", "NIU")]
    [InlineData("NIU", "NIU")]
    [InlineData("LTR", "LTR")]
    [InlineData("litro", "LTR")]
    [InlineData("DZN", "DZN")]
    [InlineData("docena", "DZN")]
    [InlineData("GLI", "GLI")]
    [InlineData("galon", "GLI")]
    [InlineData(null, "NIU")]
    [InlineData("", "NIU")]
    [InlineData("   ", "NIU")]
    [InlineData("ZZ", "ZZ")]
    public void MapearUnidadMedidaSunat_RetornaCodigoSunatEsperado(string? entrada, string esperado)
    {
        var resultado = PedidosService.MapearUnidadMedidaSunat(entrada);
        Assert.Equal(esperado, resultado);
    }
}
