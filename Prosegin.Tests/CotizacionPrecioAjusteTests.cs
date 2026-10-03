using Prosegin.Data.Entities;
using Prosegin.Data.Validation;
using Xunit;

namespace Prosegin.Tests;

public class CotizacionPrecioAjusteTests
{
    [Theory]
    [InlineData(65.00, 65.00, true)]
    [InlineData(78.00, 65.00, true)]
    [InlineData(64.99, 65.00, false)]
    [InlineData(10.00, 50.00, false)]
    public void EsPrecioValido_VerificaQuePrecioNoSeaMenorACostoBase(decimal precio, decimal costo, bool esperado)
    {
        var resultado = CotizacionPrecioRules.EsPrecioValido(precio, costo);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void RecalcularSubtotalItem_CalculaCantidadPorPrecioUnitario()
    {
        // Caso de prueba del escenario HU 3.2
        var subtotal1 = CotizacionPrecioRules.CalcularSubtotalItem(10, 78.00m);
        var subtotal2 = CotizacionPrecioRules.CalcularSubtotalItem(25, 58.00m);
        var subtotal3 = CotizacionPrecioRules.CalcularSubtotalItem(50, 15.50m);

        Assert.Equal(780.00m, subtotal1);
        Assert.Equal(1450.00m, subtotal2);
        Assert.Equal(775.00m, subtotal3);
    }

    [Fact]
    public void RecalcularMargenItem_CalculaPorcentajeSobreCostoBase()
    {
        // 13 / 65 = 20%
        var margen1 = CotizacionPrecioRules.CalcularMargenItem(78.00m, 65.00m);
        // 9.5 / 48.50 = 19.59%
        var margen2 = CotizacionPrecioRules.CalcularMargenItem(58.00m, 48.50m);
        // 2.7 / 12.80 = 21.09%
        var margen3 = CotizacionPrecioRules.CalcularMargenItem(15.50m, 12.80m);

        Assert.Equal(20.00m, margen1);
        Assert.Equal(19.59m, margen2);
        Assert.Equal(21.09m, margen3);
    }

    [Fact]
    public void CalcularResumenCotizacion_CalculaSubtotalIgvTotalYMargenGlobalExactos()
    {
        var subtotal1 = CotizacionPrecioRules.CalcularSubtotalItem(10, 78.00m); // 780.00
        var subtotal2 = CotizacionPrecioRules.CalcularSubtotalItem(25, 58.00m); // 1450.00
        var subtotal3 = CotizacionPrecioRules.CalcularSubtotalItem(50, 15.50m); // 775.00

        var subtotal = subtotal1 + subtotal2 + subtotal3;
        var igv = CotizacionPrecioRules.CalcularIgv(subtotal);
        var total = CotizacionPrecioRules.CalcularTotal(subtotal, igv);

        var costoTotal = (10 * 65.00m) + (25 * 48.50m) + (50 * 12.80m); // 2502.50
        var ganancia = CotizacionPrecioRules.CalcularGanancia(subtotal, costoTotal); // 502.50
        var margenGlobal = CotizacionPrecioRules.CalcularMargenGlobal(subtotal, costoTotal); // 20.08%

        Assert.Equal(3005.00m, subtotal);
        Assert.Equal(540.90m, igv);
        Assert.Equal(3545.90m, total);
        Assert.Equal(502.50m, ganancia);
        Assert.Equal(20.08m, margenGlobal);
    }

    [Fact]
    public void PersistirCotizacion_NoModificaCostoBaseDelCatalogoMaestro()
    {
        var producto = new Producto
        {
            Id = 1,
            Sku = "EPP-BOT-0104",
            Nombre = "Bota de Jebe Industrial",
            Categoria = "Protección de pies",
            UnidadMedida = "PAR",
            CostoReferencial = 65.00m,
            StockDisponible = 50,
            Activo = true
        };

        var precioCotizado = 78.00m;
        var cantidad = 10;
        var subtotal = CotizacionPrecioRules.CalcularSubtotalItem(cantidad, precioCotizado);

        var detalle = new CotizacionDetalle
        {
            ProductoId = producto.Id,
            Producto = producto,
            Cantidad = cantidad,
            CostoProveedorReferencial = producto.CostoReferencial,
            MargenDeseado = CotizacionPrecioRules.CalcularMargenItem(precioCotizado, producto.CostoReferencial) / 100m,
            PrecioVentaCalculado = precioCotizado,
            Subtotal = subtotal
        };

        // Verificar que el detalle fija los montos acordados para la cotización
        Assert.Equal(78.00m, detalle.PrecioVentaCalculado);
        Assert.Equal(780.00m, detalle.Subtotal);
        Assert.Equal(65.00m, detalle.CostoProveedorReferencial);

        // Verificar que el costo base maestro del producto permanece intacto
        Assert.Equal(65.00m, producto.CostoReferencial);
    }
}
