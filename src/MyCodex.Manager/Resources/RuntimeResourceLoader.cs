using System.Reflection;

namespace MyCodex.Manager.Resources;

internal static class RuntimeResourceLoader
{
    public static string Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(
                               "MyCodex.Manager.Resources.mycodex.runtime.js")
                           ?? throw new InvalidOperationException(
                               "Embedded MyCodex runtime bundle is missing.");
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
}
