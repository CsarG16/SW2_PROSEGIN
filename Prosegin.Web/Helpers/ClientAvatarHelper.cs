using System.Text.RegularExpressions;

namespace Prosegin.Web.Helpers;

public static class ClientAvatarHelper
{
    private static readonly HashSet<string> GenericEmailProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com",
        "hotmail.com", "hotmail.es",
        "outlook.com", "outlook.es",
        "yahoo.com", "yahoo.es", "ymail.com",
        "live.com", "live.com.pe",
        "icloud.com", "me.com", "mac.com",
        "proton.me", "protonmail.com",
        "msn.com",
        "aol.com",
        "zoho.com",
        "mail.com"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "del", "la", "las", "el", "los", "y", "e", "en", "por", "para"
    };

    /// <summary>
    /// Extrae el dominio corporativo del correo electrónico.
    /// Si el correo es de un proveedor genérico o es inválido, retorna null.
    /// </summary>
    public static string? ExtractCorporateDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return null;
        }

        var parts = email.Split('@', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        var domain = parts[1].Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(domain) || !domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.'))
        {
            return null;
        }

        if (GenericEmailProviders.Contains(domain))
        {
            return null;
        }

        return domain;
    }

    /// <summary>
    /// Genera la URL de Google Favicon con resolución de 128px para el dominio corporativo.
    /// Retorna null si no hay un dominio corporativo válido.
    /// </summary>
    public static string? GetCorporateFaviconUrl(string? email)
    {
        var domain = ExtractCorporateDomain(email);
        if (string.IsNullOrEmpty(domain))
        {
            return null;
        }

        return $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(domain)}&sz=128";
    }

    /// <summary>
    /// Genera las iniciales para el monograma a partir de la razón social.
    /// Limpia sufijos societarios comunes con regex y omite conectores,
    /// tomando la primera letra de las dos primeras palabras significativas.
    /// </summary>
    public static string GetInitials(string? businessName)
    {
        if (string.IsNullOrWhiteSpace(businessName))
        {
            return "CL";
        }

        // 1. Limpiar sufijos societarios comunes
        var cleaned = Regex.Replace(
            businessName,
            @"\b(S\.?A\.?C\.?|S\.?R\.?L\.?|S\.?A\.?|E\.?I\.?R\.?L\.?|S\.?A\.?A\.?|SOCIEDAD\s+ANONIMA(\s+CERRADA)?|SOCIEDAD\s+COMERCIAL\s+DE\s+RESPONSABILIDAD\s+LIMITADA|EMPRESA\s+INDIVIDUAL\s+DE\s+RESPONSABILIDAD\s+LIMITADA)\b",
            "",
            RegexOptions.IgnoreCase
        );

        // 2. Limpiar caracteres especiales
        cleaned = Regex.Replace(cleaned, @"[^\w\s]", " ");

        // 3. Omitir conectores comunes
        var words = cleaned.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(w => !StopWords.Contains(w) && w.Length > 0)
                           .ToList();

        if (words.Count == 0)
        {
            var fallback = businessName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fallback.Length > 0 && fallback[0].Length >= 2)
            {
                return fallback[0].Substring(0, 2).ToUpperInvariant();
            }
            return "CL";
        }

        if (words.Count == 1)
        {
            var single = words[0];
            return single.Length >= 2
                ? single.Substring(0, 2).ToUpperInvariant()
                : single.ToUpperInvariant();
        }

        return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
    }

    /// <summary>
    /// Toma el identificador (RUC o nombre) y calcula un hash numérico determinista
    /// para seleccionar una de las 5 paletas de color accesibles definidas.
    /// </summary>
    public static string GetColorSchemeClass(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            identifier = "PROSEGIN";
        }

        int hash = 0;
        foreach (char c in identifier)
        {
            hash = (hash * 31 + c) & 0x7FFFFFFF;
        }

        int index = hash % 5;
        return index switch
        {
            0 => "avatar-scheme-slate",
            1 => "avatar-scheme-emerald",
            2 => "avatar-scheme-amber",
            3 => "avatar-scheme-violet",
            4 => "avatar-scheme-indigo",
            _ => "avatar-scheme-slate"
        };
    }
}
