namespace MyCodex.Configuration;

// Validates palette readability after alpha compositing over a known host surface.
internal static class ColorContrast
{
    public static double Calculate(
        string foreground,
        string background,
        string hostBackground)
    {
        var host = Parse(hostBackground);
        var effectiveBackground = Composite(Parse(background), host);
        var effectiveForeground = Composite(Parse(foreground), effectiveBackground);
        var foregroundLuminance = Luminance(effectiveForeground);
        var backgroundLuminance = Luminance(effectiveBackground);
        return (Math.Max(foregroundLuminance, backgroundLuminance) + 0.05) /
               (Math.Min(foregroundLuminance, backgroundLuminance) + 0.05);
    }

    private static Rgba Parse(string value)
    {
        if (value.Length is not (7 or 9) || value[0] != '#')
        {
            throw new ArgumentException("Color must be #RRGGBB or #RRGGBBAA.");
        }
        return new Rgba(
            Convert.ToByte(value.Substring(1, 2), 16) / 255d,
            Convert.ToByte(value.Substring(3, 2), 16) / 255d,
            Convert.ToByte(value.Substring(5, 2), 16) / 255d,
            value.Length == 9
                ? Convert.ToByte(value.Substring(7, 2), 16) / 255d
                : 1d);
    }

    private static Rgba Composite(Rgba foreground, Rgba background)
    {
        var alpha = foreground.Alpha + background.Alpha * (1 - foreground.Alpha);
        if (alpha <= 0)
        {
            return new Rgba(0, 0, 0, 0);
        }
        return new Rgba(
            (foreground.Red * foreground.Alpha +
             background.Red * background.Alpha * (1 - foreground.Alpha)) / alpha,
            (foreground.Green * foreground.Alpha +
             background.Green * background.Alpha * (1 - foreground.Alpha)) / alpha,
            (foreground.Blue * foreground.Alpha +
             background.Blue * background.Alpha * (1 - foreground.Alpha)) / alpha,
            alpha);
    }

    private static double Luminance(Rgba color) =>
        0.2126 * Linear(color.Red) +
        0.7152 * Linear(color.Green) +
        0.0722 * Linear(color.Blue);

    private static double Linear(double component) =>
        component <= 0.04045
            ? component / 12.92
            : Math.Pow((component + 0.055) / 1.055, 2.4);

    private sealed record Rgba(
        double Red,
        double Green,
        double Blue,
        double Alpha);
}
