namespace Prosegin.Web.Services.Supabase;

public interface ISupabaseStorageService
{
    /// <summary>
    /// Sube un archivo IFormFile a Supabase Storage y retorna su URL pública permanente.
    /// </summary>
    Task<string> SubirArchivoAsync(IFormFile archivo, string carpeta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sube un flujo binario (Stream) a Supabase Storage y retorna su URL pública permanente.
    /// </summary>
    Task<string> SubirArchivoAsync(Stream stream, string nombreArchivo, string carpeta, string contentType = "application/pdf", CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un archivo de Supabase Storage dado su URL pública o ruta relativa dentro del bucket.
    /// </summary>
    Task<bool> EliminarArchivoAsync(string urlOPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Construye la URL pública permanente para un archivo en una carpeta específica del bucket.
    /// </summary>
    string ObtenerUrlPublica(string carpeta, string nombreArchivo);
}
