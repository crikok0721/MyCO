namespace MyCO.Manager.Services;

internal readonly record struct AvatarCropRect(
    int X,
    int Y,
    int Width,
    int Height);

// Pixel-space calculations are kept independent from WPF so crop behavior can
// be tested without opening a window.
internal static class AvatarCropMath
{
    public const double MinimumZoom = 1d;
    public const double MaximumZoom = 4d;

    public static double CoverScale(
        int sourceWidth,
        int sourceHeight,
        double viewportSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportSize);
        return Math.Max(
            viewportSize / sourceWidth,
            viewportSize / sourceHeight);
    }

    public static (double X, double Y) ClampOffset(
        int sourceWidth,
        int sourceHeight,
        double viewportSize,
        double zoom,
        double offsetX,
        double offsetY)
    {
        var scale = CoverScale(sourceWidth, sourceHeight, viewportSize) *
                    Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        var maxX = Math.Max(0d, (sourceWidth * scale - viewportSize) / 2d);
        var maxY = Math.Max(0d, (sourceHeight * scale - viewportSize) / 2d);
        return (
            Math.Clamp(offsetX, -maxX, maxX),
            Math.Clamp(offsetY, -maxY, maxY));
    }

    public static AvatarCropRect CalculateSourceCrop(
        int sourceWidth,
        int sourceHeight,
        double viewportSize,
        double zoom,
        double offsetX,
        double offsetY)
    {
        var clampedZoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        var (clampedX, clampedY) = ClampOffset(
            sourceWidth,
            sourceHeight,
            viewportSize,
            clampedZoom,
            offsetX,
            offsetY);
        var scale = CoverScale(sourceWidth, sourceHeight, viewportSize) *
                    clampedZoom;
        var side = Math.Clamp(
            (int)Math.Round(viewportSize / scale),
            1,
            Math.Min(sourceWidth, sourceHeight));
        var centerX = sourceWidth / 2d - clampedX / scale;
        var centerY = sourceHeight / 2d - clampedY / scale;
        var x = (int)Math.Round(centerX - side / 2d);
        var y = (int)Math.Round(centerY - side / 2d);
        x = Math.Clamp(x, 0, sourceWidth - side);
        y = Math.Clamp(y, 0, sourceHeight - side);
        return new AvatarCropRect(x, y, side, side);
    }
}
