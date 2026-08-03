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
        Assert.Equal(4, settingsCards.Count(element => HasStyle(element, "CardBorder")));
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
            .Where(element => Attribute(element, "Grid.Row") is "2" or "3")
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
    public void SettingsExposeAssociationAndUpdateSurfaces()
    {
        var settings = ReadManagerXaml("Views", "SettingsPage.xaml");
        var switches = Elements(settings, "CheckBox")
            .Where(element => HasStyle(element, "SwitchCheckBox"))
            .ToArray();

        Assert.Equal(3, switches.Length);
        Assert.Contains(
            switches,
            element => element.Descendants().Any(descendant =>
                Attribute(descendant, "Text")?.Contains(
                    "AssociateCodexLaunches",
                    StringComparison.Ordinal) == true));
        Assert.Contains(
            Elements(settings, "Button"),
            element => Attribute(element, "Command")?.Contains(
                "CheckForUpdatesCommand",
                StringComparison.Ordinal) == true);
        Assert.Contains(
            Elements(settings, "TextBlock"),
            element => Attribute(element, "Text")?.Contains(
                "CurrentVersionLabel",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ResetConfirmationUsesVerticalListsAndKeepsDangerousActionNonDefault()
    {
        var reset = ReadManagerXaml("Views", "ResetConfirmationWindow.xaml");
        var cancel = Elements(reset, "Button")
            .Single(element => Attribute(element, "Name") == "CancelButton");
        var confirm = Elements(reset, "Button")
            .Single(element => Attribute(element, "Click") == "Confirm_Click");

        Assert.Equal("True", Attribute(cancel, "IsDefault"));
        Assert.Equal("True", Attribute(cancel, "IsCancel"));
        Assert.Null(Attribute(confirm, "IsDefault"));
        Assert.Equal(
            4,
            Elements(reset, "TextBlock")
                .Count(element => Attribute(element, "Text")?.Contains(
                    "FactoryResetDelete",
                    StringComparison.Ordinal) == true &&
                    !Attribute(element, "Text")!.Contains(
                        "FactoryResetDeletesTitle",
                        StringComparison.Ordinal)));
        Assert.Equal(
            3,
            Elements(reset, "TextBlock")
                .Count(element => Attribute(element, "Text")?.Contains(
                    "FactoryResetKeep",
                    StringComparison.Ordinal) == true &&
                    !Attribute(element, "Text")!.Contains(
                        "FactoryResetKeepsTitle",
                        StringComparison.Ordinal)));
        Assert.Empty(Elements(reset, "LinearGradientBrush"));
    }

    [Fact]
    public void SimplifiedChineseResetConfirmationUsesTheApprovedUserCopy()
    {
        var document = ReadManagerXaml("Resources", "Strings.zh-CN.xaml");
        var values = Elements(document, "String")
            .Where(element => Attribute(element, "Key") is
                "FactoryResetConfirmTitle" or
                "FactoryResetConfirmDescription" or
                "FactoryResetDeleteAppearance" or
                "FactoryResetDeleteCalibration" or
                "FactoryResetDeleteDiagnostics" or
                "FactoryResetDeleteStartup" or
                "FactoryResetKeepProgram" or
                "FactoryResetKeepCodexProgram" or
                "FactoryResetKeepCodexData")
            .ToDictionary(
                element => Attribute(element, "Key")!,
                element => element.Value,
                StringComparer.Ordinal);

        Assert.Equal("恢复 MyCO 默认设置？", values["FactoryResetConfirmTitle"]);
        Assert.Equal(
            "MyCO 会先安全撤销对 Codex 外观的临时调整，然后重置由 MyCO 保存的本地设置。",
            values["FactoryResetConfirmDescription"]);
        Assert.Equal("角色、头像与外观设置", values["FactoryResetDeleteAppearance"]);
        Assert.Equal("校准信息", values["FactoryResetDeleteCalibration"]);
        Assert.Equal("MyCO 保存的诊断记录和备份", values["FactoryResetDeleteDiagnostics"]);
        Assert.Equal("MyCO 的开机启动与 Codex 关联设置", values["FactoryResetDeleteStartup"]);
        Assert.Equal("MyCO 程序文件", values["FactoryResetKeepProgram"]);
        Assert.Equal("Codex 程序", values["FactoryResetKeepCodexProgram"]);
        Assert.Equal(
            "Codex 账号、聊天记录和登录信息",
            values["FactoryResetKeepCodexData"]);
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
        Assert.Contains("<MyCOConfigSchemaVersion>5</MyCOConfigSchemaVersion>", version);
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
