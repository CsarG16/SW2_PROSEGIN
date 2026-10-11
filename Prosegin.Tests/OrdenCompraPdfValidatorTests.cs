using System.Text;
using PdfSharp.Pdf;
using Prosegin.Web.Services;
using Xunit;

namespace Prosegin.Tests;

public class OrdenCompraPdfValidatorTests
{
    [Fact]
    public async Task ValidarAsync_AceptaPdfConExtensionEnMayusculas()
    {
        await using var contenido = CrearPdfValido();

        var error = await OrdenCompraPdfValidator.ValidarAsync(
            "orden.PDF",
            contenido.Length,
            contenido);

        Assert.Null(error);
    }

    [Theory]
    [InlineData("orden.txt", 100, "%PDF-1.7")]
    [InlineData("orden.pdf", 100, "no es pdf")]
    [InlineData("orden.pdf", 0, "%PDF-1.7")]
    [InlineData("orden.pdf", 15, "%PDF-1.7 roto")]
    public async Task ValidarAsync_RechazaArchivosQueNoSonPdf(
        string nombre,
        long tamano,
        string contenidoTexto)
    {
        await using var contenido = new MemoryStream(Encoding.ASCII.GetBytes(contenidoTexto));

        var error = await OrdenCompraPdfValidator.ValidarAsync(nombre, tamano, contenido);

        Assert.NotNull(error);
    }

    [Fact]
    public async Task ValidarAsync_RechazaArchivoMayorA10Mb()
    {
        await using var contenido = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7"));

        var error = await OrdenCompraPdfValidator.ValidarAsync(
            "orden.pdf",
            OrdenCompraPdfValidator.TamanoMaximoBytes + 1,
            contenido);

        Assert.Equal("El archivo no puede superar los 10 MB.", error);
    }

    private static MemoryStream CrearPdfValido()
    {
        var document = new PdfDocument();
        document.AddPage();
        var contenido = new MemoryStream();
        document.Save(contenido, false);
        contenido.Position = 0;
        return contenido;
    }
}
