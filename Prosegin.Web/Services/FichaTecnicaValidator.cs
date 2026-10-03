using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Prosegin.Web.Services;

public static class FichaTecnicaValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const string MensajeError = "El archivo no cumple con el formato o tamaño admitido";

    public static async Task<bool> EsValidaAsync(IFormFile archivo, CancellationToken cancellationToken)
    {
        if (archivo.Length < 8 || archivo.Length > MaxBytes
            || !string.Equals(Path.GetExtension(archivo.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Se inspecciona el contenido; ni la extensión ni el MIME acreditan el formato.
        await using var stream = archivo.OpenReadStream();
        using var contenido = new MemoryStream();
        await stream.CopyToAsync(contenido, cancellationToken);
        if (contenido.Length != archivo.Length)
        {
            return false;
        }

        var bytes = contenido.GetBuffer();
        var cabecera = Encoding.ASCII.GetString(bytes, 0, 8);
        var longitudFinal = (int)Math.Min(contenido.Length, 1024);
        var final = Encoding.ASCII.GetString(bytes, (int)contenido.Length - longitudFinal, longitudFinal);
        return Regex.IsMatch(cabecera, @"^%PDF-(1\.[0-7]|2\.0)$")
            && final.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal);
    }
}
