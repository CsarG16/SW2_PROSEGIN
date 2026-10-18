using PdfSharp.Pdf.IO;
using Prosegin.Data.Entities;
using Prosegin.Web.Services;
using Xunit;

namespace Prosegin.Tests;

public class CotizacionPdfGeneratorTests
{
    [Fact]
    public void Generate_CreaPdfConEnlaceWebCorrectoParaFichaTecnica()
    {
        var supabaseUrl = "https://vduewkhxxqpdhvfubyfn.supabase.co/storage/v1/object/public/prosegin-pdfs/CAS-3M-H700.pdf";
        var cotizacion = new Cotizacion
        {
            Id = 1,
            Correlativo = "COT-2026-0001",
            FechaEmision = DateTime.UtcNow,
            CondicionPago = "Contado",
            Subtotal = 100m,
            Igv = 18m,
            Total = 118m,
            Cliente = new Cliente
            {
                Id = 10,
                Ruc = "20100047218",
                RazonSocial = "MINERA AURIFERA S.A.C.",
                DireccionFiscal = "Av. Javier Prado 1234"
            },
            Detalles = new List<CotizacionDetalle>
            {
                new()
                {
                    Id = 1,
                    ProductoId = 101,
                    NombreProducto = "CASCO DE SEGURIDAD 3M H-700",
                    Cantidad = 10,
                    PrecioVentaCalculado = 10m,
                    Subtotal = 100m,
                    Producto = new Producto
                    {
                        Id = 101,
                        Sku = "CAS-3M-H700",
                        Nombre = "CASCO DE SEGURIDAD 3M H-700",
                        UnidadMedida = "UND",
                        RutaFichaTecnicaPdf = supabaseUrl
                    }
                }
            }
        };

        var pdfBytes = CotizacionPdfGenerator.Generate(cotizacion, id => supabaseUrl);

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);

        using var ms = new MemoryStream(pdfBytes);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly);

        Assert.True(doc.PageCount >= 1);
        var page = doc.Pages[0];
        Assert.True(page.Annotations.Count > 0, "El PDF debe contener la anotación de enlace para la ficha técnica");
    }

    [Fact]
    public void Generate_ConProductosDeNombreYSkuLargo_GeneraPdfSinErroresNiExcepciones()
    {
        var cotizacion = new Cotizacion
        {
            Id = 2,
            Correlativo = "COT-2026-0002",
            FechaEmision = DateTime.UtcNow,
            CondicionPago = "Crédito 30 días",
            Subtotal = 500m,
            Igv = 90m,
            Total = 590m,
            Cliente = new Cliente
            {
                Id = 11,
                Ruc = "20601234567",
                RazonSocial = "EMPRESA DE TRANSPORTES Y CONSTRUCCIÓN DE OBRAS CIVILES E INDUSTRIALES DEL PERÚ S.A.C.",
                DireccionFiscal = "Carretera Central Km 15.5, Parque Industrial Huachipa, Lurigancho - Lima"
            },
            Detalles = new List<CotizacionDetalle>
            {
                new()
                {
                    Id = 1,
                    ProductoId = 101,
                    NombreProducto = "CASCO DE SEGURIDAD INDUSTRIAL 3M H-700 CON SUSPENSIÓN RATCHET SECUREFIT Y RANURAS DE VENTILACIÓN LATERALES",
                    Cantidad = 50,
                    PrecioVentaCalculado = 10m,
                    Subtotal = 500m,
                    Producto = new Producto
                    {
                        Id = 101,
                        Sku = "CAS-3M-H700-RATCHET-SECUREFIT-EXTRA-LARGO",
                        Nombre = "CASCO DE SEGURIDAD INDUSTRIAL 3M H-700 CON SUSPENSIÓN RATCHET SECUREFIT Y RANURAS DE VENTILACIÓN LATERALES",
                        UnidadMedida = "UND",
                        RutaFichaTecnicaPdf = "https://vduewkhxxqpdhvfubyfn.supabase.co/storage/v1/object/public/prosegin-pdfs/CAS-3M-H700.pdf"
                    }
                },
                new()
                {
                    Id = 2,
                    ProductoId = 102,
                    NombreProducto = "TAPONES AUDITIVOS REUTILIZABLES CON CORDÓN",
                    Cantidad = 100,
                    PrecioVentaCalculado = 2.5m,
                    Subtotal = 250m,
                    Producto = new Producto
                    {
                        Id = 102,
                        Sku = "AUD-TAP-01",
                        Nombre = "TAPONES AUDITIVOS REUTILIZABLES CON CORDÓN",
                        UnidadMedida = "PAR",
                        RutaFichaTecnicaPdf = null // sin ficha
                    }
                }
            }
        };

        var pdfBytes = CotizacionPdfGenerator.Generate(cotizacion, id => null);

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);

        using var ms = new MemoryStream(pdfBytes);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly);
        Assert.True(doc.PageCount >= 1);
        Assert.True(doc.Pages[0].Annotations.Count == 1, "Debe tener 1 enlace solo para el producto que cuenta con ficha");
    }
}
