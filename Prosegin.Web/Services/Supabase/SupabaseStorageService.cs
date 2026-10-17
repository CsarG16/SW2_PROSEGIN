using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Prosegin.Web.Services.Supabase;

public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseSettings _settings;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(
        HttpClient httpClient,
        IOptions<SupabaseSettings> options,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<string> SubirArchivoAsync(IFormFile archivo, string carpeta, CancellationToken cancellationToken = default)
    {
        if (archivo == null || archivo.Length == 0)
        {
            throw new ArgumentException("El archivo a subir no puede ser nulo ni estar vacío.", nameof(archivo));
        }

        var contentType = string.IsNullOrWhiteSpace(archivo.ContentType)
            ? "application/pdf"
            : archivo.ContentType;

        await using var stream = archivo.OpenReadStream();
        return await SubirArchivoAsync(stream, archivo.FileName, carpeta, contentType, cancellationToken);
    }

    public async Task<string> SubirArchivoAsync(
        Stream stream,
        string nombreArchivo,
        string carpeta,
        string contentType = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        ValidarConfiguracion();

        var baseUrl = NormalizarBaseUrl(_settings.Url);
        var bucket = _settings.BucketName.Trim();
        var carpetaLimpia = carpeta?.Trim().Trim('/') ?? string.Empty;

        // Generar nombre único para evitar colisiones y caracteres inválidos en URLs
        var extension = Path.GetExtension(nombreArchivo);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".pdf";
        }
        var nombreUnico = $"{Guid.NewGuid():N}{extension}";
        var objectPath = string.IsNullOrWhiteSpace(carpetaLimpia)
            ? nombreUnico
            : $"{carpetaLimpia}/{nombreUnico}";

        var requestUri = $"{baseUrl}/storage/v1/object/{bucket}/{objectPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

        // Clave de autenticación para bypass de RLS en el almacenamiento
        var authKey = !string.IsNullOrWhiteSpace(_settings.ServiceRoleKey)
            ? _settings.ServiceRoleKey.Trim()
            : _settings.PublishableKey.Trim();

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authKey);
        request.Headers.TryAddWithoutValidation("apikey", authKey);
        request.Headers.TryAddWithoutValidation("x-upsert", "true");

        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = content;

        _logger.LogInformation("Subiendo archivo a Supabase Storage: {Bucket}/{ObjectPath}", bucket, objectPath);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Error al subir archivo a Supabase Storage [{StatusCode}]: {ErrorBody}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Error al subir el archivo a Supabase Storage ({response.StatusCode}): {errorBody}");
        }

        var urlPublica = $"{baseUrl}/storage/v1/object/public/{bucket}/{objectPath}";
        _logger.LogInformation("Archivo subido con éxito a Supabase Storage. URL pública: {UrlPublica}", urlPublica);

        return urlPublica;
    }

    public async Task<bool> EliminarArchivoAsync(string urlOPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(urlOPath))
        {
            return false;
        }

        try
        {
            ValidarConfiguracion();

            var baseUrl = NormalizarBaseUrl(_settings.Url);
            var bucket = _settings.BucketName.Trim();
            var objectPath = ExtraerObjectPath(urlOPath, bucket);

            if (string.IsNullOrWhiteSpace(objectPath))
            {
                return false;
            }

            var requestUri = $"{baseUrl}/storage/v1/object/{bucket}/{objectPath}";
            using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);

            var authKey = !string.IsNullOrWhiteSpace(_settings.ServiceRoleKey)
                ? _settings.ServiceRoleKey.Trim()
                : _settings.PublishableKey.Trim();

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authKey);
            request.Headers.TryAddWithoutValidation("apikey", authKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("No se pudo eliminar el archivo en Supabase Storage [{StatusCode}]: {ErrorBody}", response.StatusCode, errorBody);
                return false;
            }

            _logger.LogInformation("Archivo eliminado exitosamente de Supabase Storage: {Bucket}/{ObjectPath}", bucket, objectPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Excepción al intentar eliminar archivo de Supabase Storage: {UrlOPath}", urlOPath);
            return false;
        }
    }

    public string ObtenerUrlPublica(string carpeta, string nombreArchivo)
    {
        ValidarConfiguracion();

        var baseUrl = NormalizarBaseUrl(_settings.Url);
        var bucket = _settings.BucketName.Trim();
        var carpetaLimpia = carpeta?.Trim().Trim('/') ?? string.Empty;
        var archivoLimpio = nombreArchivo?.Trim().TrimStart('/') ?? string.Empty;

        var path = string.IsNullOrWhiteSpace(carpetaLimpia)
            ? archivoLimpio
            : $"{carpetaLimpia}/{archivoLimpio}";

        return $"{baseUrl}/storage/v1/object/public/{bucket}/{path}";
    }

    private static string NormalizarBaseUrl(string url)
    {
        var clean = url?.Trim().TrimEnd('/') ?? string.Empty;
        if (clean.EndsWith("/rest/v1", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^"/rest/v1".Length].TrimEnd('/');
        }
        return clean;
    }

    private void ValidarConfiguracion()
    {
        if (string.IsNullOrWhiteSpace(_settings.Url) || _settings.Url.Contains("TU_PROYECTO"))
        {
            throw new InvalidOperationException("La URL de Supabase no ha sido configurada. Revisa la clave 'SupabaseSettings:Url' en appsettings.json o variables de entorno.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ServiceRoleKey) && string.IsNullOrWhiteSpace(_settings.PublishableKey))
        {
            throw new InvalidOperationException("Las credenciales de Supabase no han sido configuradas. Configura 'ServiceRoleKey' en 'SupabaseSettings'.");
        }
    }

    private static string ExtraerObjectPath(string urlOPath, string bucket)
    {
        var clean = urlOPath.Trim();

        // Si es una URL completa pública: https://xyz.supabase.co/storage/v1/object/public/{bucket}/...
        var marker = $"/storage/v1/object/public/{bucket}/";
        var idx = clean.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return clean[(idx + marker.Length)..];
        }

        // Si es una ruta relativa que empieza con el bucket o con barra
        clean = clean.TrimStart('/');
        if (clean.StartsWith($"{bucket}/", StringComparison.OrdinalIgnoreCase))
        {
            return clean[(bucket.Length + 1)..];
        }

        return clean;
    }
}
