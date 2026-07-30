using System.Security.Cryptography;

namespace MyCO.Diagnostics;

// Generates a correlation code safe to show in localized user-facing dialogs.
public static class ErrorCodeFactory
{
    public static string Create(string component, string category)
    {
        var safeComponent = Normalize(component, 8);
        var safeCategory = Normalize(category, 12);
        var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
        return $"MCX-{safeComponent}-{safeCategory}-{random}";
    }

    private static string Normalize(string value, int maximum)
    {
        var normalized = new string(value
            .Where(char.IsAsciiLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .Take(maximum)
            .ToArray());
        return string.IsNullOrEmpty(normalized) ? "GENERAL" : normalized;
    }
}
