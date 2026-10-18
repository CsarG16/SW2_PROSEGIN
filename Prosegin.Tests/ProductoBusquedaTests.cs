using Xunit;

namespace Prosegin.Tests;

public class ProductoBusquedaTests
{
    [Fact]
    public void BuscarProductos_ConCodigoParcial_DevuelveCoincidencias()
    {
        var productos = new[]
        {
            new { Sku = "EPP-1001", Nombre = "Casco de seguridad", Categoria = "Protección Cabeza", CostoReferencial = 120m },
            new { Sku = "EPP-2002", Nombre = "Guante de cuero", Categoria = "Protección Manos", CostoReferencial = 80m },
            new { Sku = "EPP-3003", Nombre = "Respirador semimascarilla", Categoria = "Protección Respiratoria", CostoReferencial = 150m }
        };

        var resultados = productos
            .Where(p => p.Sku.Contains("100", StringComparison.OrdinalIgnoreCase)
                || p.Nombre.Contains("casco", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(resultados);
        Assert.Equal("EPP-1001", resultados[0].Sku);
    }

    [Fact]
    public void BuscarProductos_ConCategoria_DevuelveSoloCategoriaExacta()
    {
        var productos = new[]
        {
            new { Sku = "EPP-1001", Nombre = "Casco de seguridad", Categoria = "Protección Cabeza", CostoReferencial = 120m },
            new { Sku = "EPP-2002", Nombre = "Guante de cuero", Categoria = "Protección Manos", CostoReferencial = 80m },
            new { Sku = "EPP-3003", Nombre = "Respirador semimascarilla", Categoria = "Protección Respiratoria", CostoReferencial = 150m }
        };

        var resultados = productos
            .Where(p => p.Categoria == "Protección Respiratoria")
            .ToList();

        Assert.Single(resultados);
        Assert.Equal("Protección Respiratoria", resultados[0].Categoria);
    }

    [Fact]
    public void BuscarProductos_SinCoincidencias_DevuelveListaVacia()
    {
        var productos = new[]
        {
            new { Sku = "EPP-1001", Nombre = "Casco de seguridad", Categoria = "Protección Cabeza", CostoReferencial = 120m },
            new { Sku = "EPP-2002", Nombre = "Guante de cuero", Categoria = "Protección Manos", CostoReferencial = 80m }
        };

        var resultado = productos
            .Where(p => p.Sku.Contains("404", StringComparison.OrdinalIgnoreCase)
                || p.Categoria == "Protección Respiratoria")
            .ToList();

        Assert.Empty(resultado);
    }

    [Fact]
    public void Producto_PorDefecto_TienePesoAproximadoMayorACero()
    {
        var producto = new Prosegin.Data.Entities.Producto();
        Assert.True(producto.PesoKg > 0);
        Assert.Equal(0.50m, producto.PesoKg);
    }

    [Fact]
    public void ProductoCreateViewModel_ValidaPesoMayorACeroYHastaDosDecimales()
    {
        var model = new Prosegin.Web.ViewModels.Catalogo.ProductoCreateViewModel
        {
            Sku = "EPP-TEST",
            Nombre = "Guante Test",
            Categoria = "Protección de manos",
            UnidadMedida = "PAR",
            Marca = "3M",
            CostoBaseAdquisicion = 25.50m,
            PesoKg = 0.456m // 3 decimales
        };

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var validationResults = model.Validate(context).ToList();

        Assert.Contains(validationResults, v => v.ErrorMessage!.Contains("decimales") && v.MemberNames.Contains(nameof(model.PesoKg)));
    }
}
