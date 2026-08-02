using MyCO.Manager.Services;

namespace MyCO.Tests;

public sealed class AvatarCropTests
{
    [Fact]
    public void LandscapeAtMinimumZoomUsesCenteredSquareCrop()
    {
        var rect = AvatarCropMath.CalculateSourceCrop(
            sourceWidth: 200,
            sourceHeight: 100,
            viewportSize: 360,
            zoom: 1,
            offsetX: 0,
            offsetY: 0);

        Assert.Equal(new AvatarCropRect(50, 0, 100, 100), rect);
    }

    [Fact]
    public void PortraitAtMinimumZoomUsesCenteredSquareCrop()
    {
        var rect = AvatarCropMath.CalculateSourceCrop(
            sourceWidth: 100,
            sourceHeight: 200,
            viewportSize: 360,
            zoom: 1,
            offsetX: 0,
            offsetY: 0);

        Assert.Equal(new AvatarCropRect(0, 50, 100, 100), rect);
    }

    [Fact]
    public void OffsetIsClampedSoTheCropNeverLeavesTheSource()
    {
        var rect = AvatarCropMath.CalculateSourceCrop(
            sourceWidth: 200,
            sourceHeight: 100,
            viewportSize: 360,
            zoom: 2,
            offsetX: 10_000,
            offsetY: -10_000);

        Assert.Equal(50, rect.Width);
        Assert.Equal(rect.Width, rect.Height);
        Assert.InRange(rect.X, 0, 100);
        Assert.InRange(rect.Y, 0, 50);
    }

    [Fact]
    public void ZoomReducesTheSourceCropWhileKeepingItSquare()
    {
        var rect = AvatarCropMath.CalculateSourceCrop(
            sourceWidth: 256,
            sourceHeight: 256,
            viewportSize: 360,
            zoom: 2,
            offsetX: 0,
            offsetY: 0);

        Assert.Equal(128, rect.Width);
        Assert.Equal(rect.Width, rect.Height);
        Assert.Equal(64, rect.X);
        Assert.Equal(64, rect.Y);
    }

    [Fact]
    public void ZoomEventsAreSubscribedOnlyAfterTheCropWindowIsInitialized()
    {
        var root = FindRepositoryRoot();
        var viewPath = Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Views",
            "AvatarCropWindow.xaml");
        var codePath = viewPath + ".cs";
        var xaml = File.ReadAllText(viewPath);
        var code = File.ReadAllText(codePath);

        Assert.DoesNotContain(
            "ValueChanged=\"ZoomSlider_ValueChanged\"",
            xaml);
        Assert.True(
            code.IndexOf("InitializeComponent();", StringComparison.Ordinal) <
            code.IndexOf(
                "ZoomSlider.ValueChanged += ZoomSlider_ValueChanged;",
                StringComparison.Ordinal));
        Assert.Contains("if (ZoomLabel is not null)", code);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyCO.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
