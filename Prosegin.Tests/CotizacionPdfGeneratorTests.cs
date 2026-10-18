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
}
