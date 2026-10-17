namespace Prosegin.Web.Services.Supabase;

public class SupabaseSettings
{
    public const string SectionName = "SupabaseSettings";

    /// <summary>
    /// URL base del proyecto Supabase (ej: https://xyzcompany.supabase.co).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Llave secreta con rol 'service_role' para operaciones seguras de backend (bypass RLS en storage).
    /// </summary>
    public string ServiceRoleKey { get; set; } = string.Empty;

    /// <summary>
    /// Llave pública 'anon' / publishable del proyecto.
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del bucket en Supabase Storage (por defecto 'prosegin-pdfs').
    /// </summary>
    public string BucketName { get; set; } = "prosegin-pdfs";
}
