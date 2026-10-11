using PdfSharp.Pdf.IO;

namespace Prosegin.Web.Services;

public static class OrdenCompraPdfValidator
{
    public const long TamanoMaximoBytes = 10 * 1024 * 1024;
    private static readonly byte[] FirmaPdf = "%PDF-"u8.ToArray();

    public static async Task<string?> ValidarAsync(
        string nombreArchivo,
        long tamanoBytes,
        Stream contenido,
        CancellationToken cancellationToken = default)
    {
        if (tamanoBytes <= 0)
        {
            return "Selecciona un archivo PDF.";
        }

        if (tamanoBytes > TamanoMaximoBytes)
        {
            return "El archivo no puede superar los 10 MB.";
        }

        if (!string.Equals(Path.GetExtension(nombreArchivo), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "La Orden de Compra debe tener formato PDF.";
        }

        await using var pdfStream = new MemoryStream();
        await contenido.CopyToAsync(pdfStream, cancellationToken);
        if (pdfStream.Length > TamanoMaximoBytes)
        {
            return "El archivo no puede superar los 10 MB.";
        }

        if (pdfStream.Length != tamanoBytes
            || pdfStream.Length < FirmaPdf.Length
            || !pdfStream.GetBuffer().AsSpan(0, FirmaPdf.Length).SequenceEqual(FirmaPdf))
        {
            return "El archivo seleccionado no es un PDF válido.";
        }

        pdfStream.Position = 0;
        try
        {
            using var document = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
            return document.PageCount > 0
                ? null
                : "El archivo seleccionado no contiene páginas PDF válidas.";
        }
        catch (PdfReaderException)
        {
            return "El archivo seleccionado no es un PDF válido.";
        }
    }
}
