// Normalizes display names before they are persisted or sent into the page runtime.
namespace MyCodex.Configuration;

public static class NicknameValidator
{
    public const int MaximumLength = 32;

    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Trim();
        if (normalized.Length is < 1 or > MaximumLength)
        {
            throw new ArgumentException(
                $"Nickname must contain between 1 and {MaximumLength} characters.",
                nameof(value));
        }
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Nickname must not contain control characters.", nameof(value));
        }
        return normalized;
    }
}
