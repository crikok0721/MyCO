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
    public void AppearanceExposesRoleSpecificAvatarAndNicknameGeometry()
    {
        var appearance = ReadManagerXaml("Views", "AppearancePage.xaml");
        var sliders = Elements(appearance, "Slider").ToArray();
        var bindings = sliders
            .Select(element => Attribute(element, "Value"))
            .Where(value => value is not null)
            .ToArray();

        foreach (var property in new[]
                 {
                     "AssistantAvatarOffsetX", "AssistantAvatarOffsetY",
                     "AssistantNicknameOffsetX", "AssistantNicknameOffsetY",
                     "UserAvatarOffsetX", "UserAvatarOffsetY",
                     "UserNicknameOffsetX", "UserNicknameOffsetY"
                 })
        {
            Assert.Single(
                bindings,
                value => value!.Contains(property, StringComparison.Ordinal));
        }
        Assert.Single(
            bindings,
            value => value!.Contains("AssistantBubbleMaxWidth", StringComparison.Ordinal));
        Assert.DoesNotContain(bindings, value =>
            value!.Contains("MessageMaxWidth", StringComparison.Ordinal) ||
            value.Contains("{Binding AvatarOffset", StringComparison.Ordinal));

        Assert.Equal(
            4,
            sliders.Count(slider =>
                Attribute(slider, "Minimum") == "-32" &&
                Attribute(slider, "Maximum") == "32"));
        Assert.Equal(
            2,
            sliders.Count(slider =>
                Attribute(slider, "Minimum") == "-20" &&
                Attribute(slider, "Maximum") == "40"));
        Assert.Equal(
            2,
            sliders.Count(slider =>
                Attribute(slider, "Minimum") == "-12" &&
                Attribute(slider, "Maximum") == "28"));
        Assert.Single(
            sliders,
            slider => Attribute(slider, "Minimum") == "45" &&
                      Attribute(slider, "Maximum") == "80");
    }

    [Fact]
    public void PreviewBindsAllRoleGeometryAndLimitsOnlyAssistantBubbleWidth()
    {
        var preview = ReadManagerXaml("Controls", "ChatPreviewControl.xaml");
        var source = preview.ToString(SaveOptions.DisableFormatting);

        foreach (var property in new[]
                 {
                     "AssistantAvatarOffsetX", "AssistantAvatarOffsetY",
                     "AssistantNicknameOffsetX", "AssistantNicknameOffsetY",
                     "UserAvatarOffsetX", "UserAvatarOffsetY",
                     "UserNicknameOffsetX", "UserNicknameOffsetY",
                     "MessageGap", "AssistantBubbleMaxWidth"
                 })
        {
            Assert.Contains(property, source, StringComparison.Ordinal);
        }

        var assistantBubble = Elements(preview, "Border")
            .Single(element => Attribute(element, "Name") == "AssistantPreviewBubble");
        var userBubble = Elements(preview, "Border")
            .Single(element => Attribute(element, "Name") == "UserPreviewBubble");
        Assert.Contains(
            "AssistantBubbleMaxWidth",
            Attribute(assistantBubble, "MaxWidth"),
            StringComparison.Ordinal);
        Assert.Null(Attribute(userBubble, "MaxWidth"));
    }

    [Fact]
    public void OnboardingUsesOnlyTheConciseLocalizedHeadingAndStableFooter()
    {
        var expectedHeadings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en-US"] = "Let's get started!",
            ["zh-CN"] = "让我们从这里开始！",
            ["zh-TW"] = "讓我們從這裡開始！",
            ["ja-JP"] = "ここから始めましょう！"
        };

        foreach (var (culture, expected) in expectedHeadings)
        {
            var resources = ReadManagerXaml("Resources", $"Strings.{culture}.xaml");
            var entries = Elements(resources, "String")
                .ToDictionary(element => Attribute(element, "Key")!, element => element.Value);
            Assert.Equal(expected, entries["OnboardingHeading"]);
            Assert.DoesNotContain("OnboardingDescription", entries.Keys);
            Assert.DoesNotContain("OnboardingPrivacy", entries.Keys);
        }

        var onboarding = ReadManagerXaml("Views", "OnboardingWindow.xaml");
        var source = onboarding.ToString(SaveOptions.DisableFormatting);
        Assert.DoesNotContain("OnboardingDescription", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnboardingPrivacy", source, StringComparison.Ordinal);
        Assert.Empty(Elements(onboarding, "Ellipse"));
        var footer = Elements(onboarding, "Border")
            .Single(element => Attribute(element, "Grid.Row") == "2");
        Assert.Equal("82", Attribute(
            footer.Parent!.Elements().First().Elements().ElementAt(2),
            "Height"));
    }

    [Fact]
    public void SaveAndApplyReportsZeroPartialAndCompleteRuntimeOutcomes()
    {
        var root = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "ViewModels",
            "MainWindowViewModel.cs"));

        Assert.Contains("RuntimeConfigApplyResult", viewModel, StringComparison.Ordinal);
        Assert.Contains("applyResult.SessionCount == 0", viewModel, StringComparison.Ordinal);
        Assert.Contains("applyResult.IsFullyApplied", viewModel, StringComparison.Ordinal);
        Assert.Contains("applyResult.AppliedCount", viewModel, StringComparison.Ordinal);
        Assert.Contains("applyResult.FailedCount", viewModel, StringComparison.Ordinal);
        Assert.Contains("StatusAppearanceSavedNoSessions", viewModel, StringComparison.Ordinal);
        Assert.Contains("StatusAppearancePartiallyAppliedFormat", viewModel, StringComparison.Ordinal);
        Assert.Contains("StatusAppearanceSavedAndAppliedFormat", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus(\"StatusAppearanceSaved\")", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void AssociatedLaunchQueuesOnceAndChecksEveryRunningCandidate()
    {
        var root = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MyCO.Manager",
            "ViewModels",
            "MainWindowViewModel.cs"));

        Assert.Contains("_associatedLaunchQueued", viewModel, StringComparison.Ordinal);
        Assert.Contains("queueWhenBusy: true", viewModel, StringComparison.Ordinal);
        Assert.Contains("Candidates.Any(candidate => candidate.IsRunning)", viewModel, StringComparison.Ordinal);
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
        Assert.Contains("<MyCOConfigSchemaVersion>6</MyCOConfigSchemaVersion>", version);
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
