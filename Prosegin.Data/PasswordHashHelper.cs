using System.Security.Cryptography;
using System.Text;

namespace Prosegin.Data;

public static class PasswordHashHelper
{
    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string password, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashedPassword))
        {
            return false;
        }

        return string.Equals(Hash(password), hashedPassword, StringComparison.OrdinalIgnoreCase);
    }
}
