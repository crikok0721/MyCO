using System.Xml.Linq;

namespace MyCO.Tests;

public sealed class ManagerUiRegressionTests
{
    [Fact]
    public void PreviewBubblesUseOneUserSurfaceWhileKeepingSeparateAlignment()
    {
        var preview = ReadManagerXaml("Controls", "ChatPreviewControl.xaml");
        var bubbles = Elements(preview, "Border")
            .Where(element => HasStyle(element, "PreviewBubbleStyle"))
            .ToArray();
        var textBlocks = bubbles
            .SelectMany(element => element.Descendants()
                .Where(descendant => descendant.Name.LocalName == "TextBlock"))
            .ToArray();

        Assert.Equal(2, bubbles.Length);
        Assert.Single(bubbles.Select(element => Attribute(element, "Background")).Distinct());
        Assert.Equal(2, textBlocks.Length);
        Assert.Single(textBlocks.Select(element => Attribute(element, "Foreground")).Distinct());
        Assert.Contains("PreviewUserBubble", Attribute(bubbles[0], "Background"));
        Assert.Contains("PreviewUserText", Attribute(textBlocks[0], "Foreground"));
        Assert.Contains(
            bubbles,
            element => Attribute(element, "HorizontalAlignment") == "Right");
    }

    [Fact]
    public void PreviewSurfaceUsesASolidSurfaceWithoutADecorativeOutline()
    {
        var preview = ReadManagerXaml("Controls", "ChatPreviewControl.xaml");
        var surface = Elements(preview, "Border")
            .Single(element =>
                Attribute(element, "Background")?.Contains(
                    "PreviewBackground",
                    StringComparison.Ordinal) == true);

        Assert.Equal("0", Attribute(surface, "BorderThickness"));
        Assert.Null(Attribute(surface, "BorderBrush"));
    }

    [Fact]
    public void HomeRemovesOnlyThePageHeaderAppearanceButton()
    {
        var home = ReadManagerXaml("Views", "HomePage.xaml");
        var buttons = Elements(home, "Button").ToArray();

        Assert.DoesNotContain(
            buttons,
            button => Attribute(button, "Content") == "{DynamicResource HomeCustomize}");
        Assert.Equal(
            2,
            buttons.Count(button =>
                Attribute(button, "Content") == "{DynamicResource HomeEditAppearance}"));
    }

    [Fact]
    public void OrdinaryManagerContentCardsUseSharedBorderlessCardStyle()
    {
        var settings = ReadManagerXaml("Views", "SettingsPage.xaml");
        var about = ReadManagerXaml("Views", "AboutPage.xaml");
        var diagnostics = ReadManagerXaml("Views", "DiagnosticsPage.xaml");
        var onboarding = ReadManagerXaml("Views", "OnboardingWindow.xaml");
        var reset = ReadManagerXaml("Views", "ResetConfirmationWindow.xaml");

        var settingsCards = Elements(settings, "Border").ToArray();
        Assert.Equal(3, settingsCards.Count(element => HasStyle(element, "CardBorder")));
        Assert.DoesNotContain(
            settingsCards,
            element => Attribute(element, "BorderThickness") == "1");

        var aboutBorders = Elements(about, "Border").ToArray();
        Assert.Contains(aboutBorders, element => HasStyle(element, "CardBorder"));
        Assert.Contains(
            aboutBorders,
            element => Attribute(element, "BorderThickness") == "1" &&
                        Attribute(element, "BorderBrush")?.Contains("AccentBorderBrush", StringComparison.Ordinal) == true);

        var diagnosticsBorders = Elements(diagnostics, "Border").ToArray();
        Assert.Equal(3, diagnosticsBorders.Count(element => HasStyle(element, "CardBorder")));
        Assert.DoesNotContain(
            diagnosticsBorders,
            element => Attribute(element, "BorderThickness") == "1");

        var onboardingSteps = Elements(onboarding, "Border")
            .Single(element => Attribute(element, "Name") == "OnboardingSteps");
        Assert.True(HasStyle(onboardingSteps, "CardBorder"));
        Assert.Contains(
            Elements(onboarding, "Border"),
            element => Attribute(element, "Name") == "RootShell" &&
                        Attribute(element, "BorderThickness") == "1");

        var resetCards = Elements(reset, "Border")
            .Where(element => Attribute(element, "Grid.Row") is "1" or "2")
            .ToArray();
        Assert.Equal(2, resetCards.Length);
        Assert.All(resetCards, element => Assert.True(HasStyle(element, "CardBorder")));
        Assert.DoesNotContain(
            resetCards,
            element => Attribute(element, "BorderThickness") == "1");
    }

    [Fact]
    public void FactoryResetDescriptionUsesTheApprovedFourLanguageCopy()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en-US"] = "Restore MyCO to its default first-launch state.",
            ["zh-CN"] = "将 MyCO 恢复到首次启动的默认状态。",
            ["zh-TW"] = "將 MyCO 恢復為首次啟動的預設狀態。",
            ["ja-JP"] = "MyCO を初回起動時の既定状態に戻します。"
        };

        foreach (var (culture, value) in expected)
        {
            var document = ReadManagerXaml("Resources", $"Strings.{culture}.xaml");
            var description = Elements(document, "String")
                .Single(element => Attribute(element, "Key") == "FactoryResetDescription")
                .Value;

            Assert.Equal(value, description);
        }
    }

    [Fact]
    public void TrayUsesCustomRoundedRendererWithoutChangingItsMenuSurfaceOwner()
    {
        var root = FindRepositoryRoot();
        var tray = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "Services",
            "TrayService.cs"));

        Assert.Contains("TrayMenuRenderer", tray);
        Assert.DoesNotContain(
            "RenderMode = Forms.ToolStripRenderMode.System",
            tray,
            StringComparison.Ordinal);
        Assert.Contains("ContextMenuStrip", tray);
    }

    [Fact]
    public void VersionSourceAdvancesOnlyTheProductVersionForThisUiRelease()
    {
        var root = FindRepositoryRoot();
        var version = File.ReadAllText(Path.Combine(
            root,
            "eng",
            "MyCO.Version.props"));

        Assert.Contains("<MyCOVersion>0.99.2</MyCOVersion>", version);
        Assert.Contains("<MyCOProtocolVersion>1</MyCOProtocolVersion>", version);
        Assert.Contains("<MyCOConfigSchemaVersion>4</MyCOConfigSchemaVersion>", version);
        Assert.Contains("<MyCOCalibrationSchemaVersion>1</MyCOCalibrationSchemaVersion>", version);
    }

    private static XDocument ReadManagerXaml(params string[] parts) =>
        XDocument.Load(Path.Combine([FindRepositoryRoot(), "src", "MyCO.Manager", .. parts]));

    private static IEnumerable<XElement> Elements(XDocument document, string localName) =>
        document.Descendants().Where(element => element.Name.LocalName == localName);

    private static bool HasStyle(XElement element, string styleKey) =>
        Attribute(element, "Style")?.Contains(styleKey, StringComparison.Ordinal) == true;

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == localName)
            ?.Value;

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
