using System.Reflection;

// Reads the bundled JavaScript runtime that is embedded by MyCO.Manager.csproj.
namespace MyCO.Manager.Resources;

internal static class RuntimeResourceLoader
{
    public static string Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(
                               "MyCO.Manager.Resources.MyCO.runtime.js")
                           ?? throw new InvalidOperationException(
                               "Embedded MyCO runtime bundle is missing.");
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
}
