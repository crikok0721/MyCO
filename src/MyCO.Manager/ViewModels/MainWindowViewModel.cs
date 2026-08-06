using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using MyCO.Applications;
using MyCO.Avatars;
using MyCO.Compatibility;
using MyCO.Configuration;
using MyCO.Cdp;
using MyCO.Injection;
using MyCO.Diagnostics;
using MyCO.Manager.Localization;
using MyCO.Manager.Resources;
using MyCO.Manager.Services;
using MyCO.Manager.Views;
using MyCO.Startup;
using MyCO.Updates;

// Main MVVM coordinator that connects WPF controls to config, discovery, and CDP sessions.
namespace MyCO.Manager.ViewModels;

public enum ManagerPage
{
    Home,
    Appearance,
    Calibration,
    Diagnostics,
    About,
    Settings
}

public enum CodexPreviewTheme
{
    Dark,
    Light
}

internal enum AvatarImportFailure
{
    Validation,
    Decode
}

internal sealed class AvatarImportException(
    AvatarImportFailure failure,
    Exception innerException)
    : Exception("The selected avatar could not be prepared safely.", innerException)
{
    public AvatarImportFailure Failure { get; } = failure;
}

public sealed class CodexPreviewThemeOption(
    CodexPreviewTheme theme,
    string displayName)
{
    public CodexPreviewTheme Theme { get; } = theme;
    public string DisplayName { get; } = displayName;
}

public sealed class ManagerThemeOption(
    ManagerThemeMode mode,
    string displayName)
{
    public ManagerThemeMode Mode { get; } = mode;
    public string DisplayName { get; } = displayName;
}

public sealed class BubbleDisplayModeOption(
    BubbleDisplayMode mode,
    string displayName)
{
    public BubbleDisplayMode Mode { get; } = mode;
    public string DisplayName { get; } = displayName;
}

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly ConfigPaths _paths = new();
    private readonly ConfigStore _configStore;
    private readonly FactoryResetService _factoryResetService;
    private readonly AvatarService _avatarService;
    private readonly WindowsApplicationLocator _locator = new();
    private readonly ApplicationAdapterCatalog _adapters = new();
    private readonly ApplicationRestartService _restartService = new();
    private readonly DesktopSessionController _controller;
    private readonly IPrivacySafeLogger _logger;
    private readonly ThemeService _themeService;
    private readonly IStartupRegistrationService _startupRegistration;
    private readonly CodexLaunchAssociationService _codexLaunchAssociation;
    private readonly UpdateCoordinator _updateCoordinator;
    private readonly TimeSpan _gracefulShutdownTimeout = TimeSpan.FromSeconds(12);
    private readonly TimeSpan _forceShutdownTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _quiescenceTimeout = TimeSpan.FromSeconds(15);
    // Serializes the close+launch transaction; see RunDesktopOperationAsync.
    private readonly SemaphoreSlim _restartGate = new(1, 1);
    // Language changes, calibration, and the Save button can race; serialize disk writes.
    private readonly SemaphoreSlim _configSaveGate = new(1, 1);
    private CalibrationConfig _calibration = new();
    private AppConfig _persistedConfig = AppConfig.Default;
    private ApplicationCandidate? _selectedCandidate;
    private LanguageOption _selectedLanguage =
        LocalizationService.SupportedLanguages[0];
    private ManagerPage _currentPage = ManagerPage.Home;
    private string _assistantName = "菲叶子";
    private string _userName = "You";
    private string _assistantAvatar = string.Empty;
    private string _userAvatar = string.Empty;
    // Appearance sliders store user deltas from the versioned Codex baseline.
    // Zero is the calibrated/safe baseline and therefore the visual midpoint.
    private double _avatarSize;
    private double _assistantAvatarOffsetX;
    private double _assistantAvatarOffsetY;
    private double _userAvatarOffsetX;
    private double _userAvatarOffsetY;
    private double _assistantNicknameOffsetX;
    private double _assistantNicknameOffsetY;
    private double _userNicknameOffsetX;
    private double _userNicknameOffsetY;
    private double _bubbleRadius;
    private double _bubblePaddingX;
    private double _bubblePaddingY;
    private double _messageGap;
    private double _assistantBubbleMaxWidth;
    private bool _nicknameVisible = true;
    private string _userBubble = "#242424";
    private string _assistantBubble = "#222222";
    private string _darkAssistantText = "#F2F2F2";
    private string _darkNicknameColor = "#9A9A9A";
    private string _darkAvatarBackground = "#303030";
    private string _darkAvatarBorder = "#FFFFFF14";
    private string _lightAssistantBubble = "#F1F3F5";
    private string _lightAssistantText = "#202124";
    private string _lightNicknameColor = "#5F6672";
    private string _lightAvatarBackground = "#E5E7EB";
    private string _lightAvatarBorder = "#00000024";
    private CodexPreviewThemeOption? _selectedCodexPreviewThemeOption;
    private ManagerThemeOption? _selectedManagerThemeOption;
    private BubbleDisplayModeOption? _selectedBubbleDisplayModeOption;
    private bool _launchAtLogin;
    private bool _launchCodexOnMycoStart;
    private bool _associateCodexLaunches;
    private string? _trayMinimizeNotificationBootId;
    private bool _previewThemeFollowsManager = true;
    private bool _settingPreviewTheme;
    private string _status = LocalizationService.Get("StatusStarting");
    private string? _statusKey = "StatusStarting";
    private object?[] _statusArguments = [];
    private string _updateStatusKey = "UpdateStatusReady";
    private object?[] _updateStatusArguments = [];
    private string _diagnosticsText =
        LocalizationService.Get("DiagnosticsNotRefreshed");
    private bool _diagnosticsGenerated;
    private bool _initialized;
    private int _desktopOperationInProgress;
    private int _associatedLaunchQueued;
    private DesktopSessionState _sessionState =
        new(false, false, false, null, 0, "Not connected", null, null);

    public MainWindowViewModel()
    {
        // Keep service construction here so the views contain no business logic.
        _configStore = new ConfigStore(_paths);
        _factoryResetService = new FactoryResetService(_paths);
        _avatarService = new AvatarService(_paths.AvatarsDirectory);
        _logger = new PrivacySafeLogger(_paths.LogsDirectory);
        _themeService = (System.Windows.Application.Current as App)?.ThemeService
                        ?? throw new InvalidOperationException(
                            "The app-level theme service is unavailable.");
        _themeService.ThemeChanged += HandleThemeChanged;
        RefreshCodexPreviewThemeOptions(
            ToPreviewTheme(_themeService.EffectiveTheme),
            followsManager: true);
        _startupRegistration = new StartupRegistrationService();
        _codexLaunchAssociation = new CodexLaunchAssociationService();
        _updateCoordinator = new UpdateCoordinator();
        _controller = new DesktopSessionController(
            RuntimeResourceLoader.Load(),
            logger: _logger);
        _controller.StateChanged += HandleStateChanged;
        _controller.RuntimeEventReceived += HandleRuntimeEvent;

        SelectHomeCommand = new RelayCommand(() => CurrentPage = ManagerPage.Home);
        SelectAppearanceCommand = new RelayCommand(() => CurrentPage = ManagerPage.Appearance);
        SelectCalibrationCommand = new RelayCommand(() => CurrentPage = ManagerPage.Calibration);
        SelectDiagnosticsCommand = new RelayCommand(() => CurrentPage = ManagerPage.Diagnostics);
        SelectAboutCommand = new RelayCommand(() => CurrentPage = ManagerPage.About);
        SelectSettingsCommand = new RelayCommand(() => CurrentPage = ManagerPage.Settings);
        DetectCommand = new AsyncRelayCommand(
            () => GuardAsync(DetectAsync, "ErrorDesktopDetection"),
            CanDetect);
        StartCommand = new AsyncRelayCommand(
            () => GuardAsync(
                () => RunDesktopOperationAsync(() => StartAsync(false)),
                "ErrorStartDesktop"),
            CanStart);
        RestartCommand = new AsyncRelayCommand(
            () => GuardAsync(
                () => RunDesktopOperationAsync(() => StartAsync(true)),
                "ErrorRestartDesktop"),
            CanRestart);
        SaveCommand = new AsyncRelayCommand(
            () => GuardAsync(SaveAndApplyAsync, "ErrorSaveAppearance"),
            CanUseDesktopSession);
        EnableCommand = new AsyncRelayCommand(() => GuardAsync(
            () => _controller.EnableSkinAsync(),
            "ErrorEnableSkin"),
            () => CanUseDesktopSession() &&
                  SessionState.IsConnected &&
                  !SessionState.IsSkinRequested);
        DisableCommand = new AsyncRelayCommand(() => GuardAsync(
            () => _controller.DisableSkinAsync(),
            "ErrorDisableSkin"),
            () => CanUseDesktopSession() &&
                  SessionState.IsConnected &&
                  SessionState.IsSkinRequested);
        PickAssistantAvatarCommand = new AsyncRelayCommand(() => GuardAsync(
            () => PickAvatarAsync(true),
            "ErrorImportAssistantAvatar"),
            CanUseDesktopSession);
        PickUserAvatarCommand = new AsyncRelayCommand(() => GuardAsync(
            () => PickAvatarAsync(false),
            "ErrorImportUserAvatar"),
            CanUseDesktopSession);
        CalibrateAssistantCommand = new AsyncRelayCommand(() => GuardAsync(
            () => _controller.StartCalibrationAsync("assistant"),
            "ErrorCalibrateAssistant"),
            CanCalibrate);
        CalibrateUserCommand = new AsyncRelayCommand(() => GuardAsync(
            () => _controller.StartCalibrationAsync("user"),
            "ErrorCalibrateUser"),
            CanCalibrate);
        RefreshDiagnosticsCommand = new AsyncRelayCommand(() => GuardAsync(
            RefreshDiagnosticsAsync,
            "ErrorReadDiagnostics"),
            () => CanUseDesktopSession() && SessionState.IsConnected);
        ResetAppearanceCommand = new RelayCommand(ResetAppearance);
        OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder);
        SaveSettingsCommand = new AsyncRelayCommand(() => GuardAsync(
            SaveSettingsAsync,
            "ErrorSaveSettings"));
        FactoryResetCommand = new AsyncRelayCommand(() => GuardAsync(
            ConfirmAndFactoryResetAsync,
            "ErrorFactoryReset"),
            CanUseDesktopSession);
        CheckForUpdatesCommand = new AsyncRelayCommand(
            () => GuardAsync(CheckForUpdatesAsync, "ErrorCheckForUpdates"));
    }

    public ObservableCollection<ApplicationCandidate> Candidates { get; } = [];
    public IReadOnlyList<LanguageOption> SupportedLanguages =>
        LocalizationService.SupportedLanguages;
    public ObservableCollection<CodexPreviewThemeOption> CodexPreviewThemeOptions { get; } =
        [];
    public ObservableCollection<ManagerThemeOption> ManagerThemeOptions { get; } = [];
    public ObservableCollection<BubbleDisplayModeOption> BubbleDisplayModeOptions { get; } =
        [];

    public ICommand SelectHomeCommand { get; }
    public ICommand SelectAppearanceCommand { get; }
    public ICommand SelectCalibrationCommand { get; }
    public ICommand SelectDiagnosticsCommand { get; }
    public ICommand SelectAboutCommand { get; }
    public ICommand SelectSettingsCommand { get; }
    public ICommand DetectCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand EnableCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand PickAssistantAvatarCommand { get; }
    public ICommand PickUserAvatarCommand { get; }
    public ICommand CalibrateAssistantCommand { get; }
    public ICommand CalibrateUserCommand { get; }
    public ICommand RefreshDiagnosticsCommand { get; }
    public ICommand ResetAppearanceCommand { get; }
    public ICommand OpenConfigFolderCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand FactoryResetCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || !Set(ref _selectedLanguage, value))
            {
                return;
            }
            // Change the UI immediately; persistence follows only after initialization.
            LocalizationService.ApplyLanguage(value.Code);
            RefreshLocalizedProperties();
            if (_initialized)
            {
                _ = PersistLanguageAsync(value.Code);
            }
        }
    }

    public ApplicationCandidate? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (Set(ref _selectedCandidate, value))
            {
                RaiseHomeActionState();
                RaiseCommandCanExecute();
            }
        }
    }

    public ManagerPage CurrentPage
    {
        get => _currentPage;
        set
        {
            if (!Set(ref _currentPage, value))
            {
                return;
            }
            Raise(nameof(IsHomePage));
            Raise(nameof(IsAppearancePage));
            Raise(nameof(IsCalibrationPage));
            Raise(nameof(IsDiagnosticsPage));
            Raise(nameof(IsAboutPage));
            Raise(nameof(IsSettingsPage));
        }
    }

    public bool IsHomePage => CurrentPage == ManagerPage.Home;
    public bool IsAppearancePage => CurrentPage == ManagerPage.Appearance;
    public bool IsCalibrationPage => CurrentPage == ManagerPage.Calibration;
    public bool IsDiagnosticsPage => CurrentPage == ManagerPage.Diagnostics;
    public bool IsAboutPage => CurrentPage == ManagerPage.About;
    public bool IsSettingsPage => CurrentPage == ManagerPage.Settings;

    public CodexPreviewThemeOption? SelectedCodexPreviewThemeOption
    {
        get => _selectedCodexPreviewThemeOption;
        set
        {
            if (value is null || !Set(ref _selectedCodexPreviewThemeOption, value))
            {
                return;
            }
            if (!_settingPreviewTheme)
            {
                _previewThemeFollowsManager = false;
            }
            RaisePreviewPalette();
        }
    }

    public bool IsDarkCodexPreview =>
        SelectedCodexPreviewThemeOption?.Theme != CodexPreviewTheme.Light;
    public string PreviewBackground =>
        IsDarkCodexPreview ? "#181513" : "#F1F5F3";
    public string PreviewBorder =>
        IsDarkCodexPreview ? "#403833" : "#D7DFDC";
    public string PreviewAssistantBubble =>
        IsDarkCodexPreview ? AssistantBubble : LightAssistantBubble;
    public string PreviewAssistantText =>
        IsDarkCodexPreview ? DarkAssistantText : LightAssistantText;
    public string PreviewNicknameColor =>
        IsDarkCodexPreview ? DarkNicknameColor : LightNicknameColor;
    public string PreviewAvatarBackground =>
        IsDarkCodexPreview ? DarkAvatarBackground : LightAvatarBackground;
    public string PreviewAvatarBorder =>
        IsDarkCodexPreview ? DarkAvatarBorder : LightAvatarBorder;
    public string PreviewUserBubble =>
        IsDarkCodexPreview ? "#2B2724" : "#E9EEEB";
    public string PreviewUserText =>
        IsDarkCodexPreview ? "#F4F0EC" : "#222A26";

    public ManagerThemeOption? SelectedManagerThemeOption
    {
        get => _selectedManagerThemeOption;
        set
        {
            if (value is null || !Set(ref _selectedManagerThemeOption, value))
            {
                return;
            }
            _themeService.ApplyMode(value.Mode);
        }
    }

    public BubbleDisplayModeOption? SelectedBubbleDisplayModeOption
    {
        get => _selectedBubbleDisplayModeOption;
        set => Set(ref _selectedBubbleDisplayModeOption, value);
    }

    public bool LaunchAtLogin
    {
        get => _launchAtLogin;
        set => Set(ref _launchAtLogin, value);
    }

    public bool LaunchCodexOnMycoStart
    {
        get => _launchCodexOnMycoStart;
        set => Set(ref _launchCodexOnMycoStart, value);
    }

    public bool AssociateCodexLaunches
    {
        get => _associateCodexLaunches;
        set => Set(ref _associateCodexLaunches, value);
    }

    public bool TryClaimTrayMinimizeNotification()
    {
        var bootId = SystemBootIdentity.Current();
        if (!TrayNotificationPolicy.ShouldNotify(
                userInitiated: true,
                bootId,
                _trayMinimizeNotificationBootId))
        {
            return false;
        }

        _trayMinimizeNotificationBootId = bootId;
        _ = PersistTrayMinimizeNotificationBootIdAsync();
        return true;
    }

    public string AssistantName
    {
        get => _assistantName;
        set => Set(ref _assistantName, value);
    }

    public string UserName
    {
        get => _userName;
        set => Set(ref _userName, value);
    }

    public string AssistantAvatar
    {
        get => _assistantAvatar;
        set => Set(ref _assistantAvatar, value);
    }

    public string UserAvatar
    {
        get => _userAvatar;
        set => Set(ref _userAvatar, value);
    }

    public double AvatarSize
    {
        get => _avatarSize;
        set
        {
            if (Set(ref _avatarSize, value))
            {
                Raise(nameof(AvatarSizeLabel));
            }
        }
    }

    public double AssistantAvatarOffsetX
    {
        get => _assistantAvatarOffsetX;
        set
        {
            if (Set(ref _assistantAvatarOffsetX, value))
            {
                Raise(nameof(AssistantAvatarOffsetXLabel));
            }
        }
    }

    public double AssistantAvatarOffsetY
    {
        get => _assistantAvatarOffsetY;
        set
        {
            if (Set(ref _assistantAvatarOffsetY, value))
            {
                Raise(nameof(AssistantAvatarOffsetYLabel));
            }
        }
    }

    public double UserAvatarOffsetX
    {
        get => _userAvatarOffsetX;
        set
        {
            if (Set(ref _userAvatarOffsetX, value))
            {
                Raise(nameof(UserAvatarOffsetXLabel));
            }
        }
    }

    public double UserAvatarOffsetY
    {
        get => _userAvatarOffsetY;
        set
        {
            if (Set(ref _userAvatarOffsetY, value))
            {
                Raise(nameof(UserAvatarOffsetYLabel));
            }
        }
    }

    public double AssistantNicknameOffsetX
    {
        get => _assistantNicknameOffsetX;
        set
        {
            if (Set(ref _assistantNicknameOffsetX, value))
            {
                Raise(nameof(AssistantNicknameOffsetXLabel));
            }
        }
    }

    public double AssistantNicknameOffsetY
    {
        get => _assistantNicknameOffsetY;
        set
        {
            if (Set(ref _assistantNicknameOffsetY, value))
            {
                Raise(nameof(AssistantNicknameOffsetYLabel));
            }
        }
    }

    public double UserNicknameOffsetX
    {
        get => _userNicknameOffsetX;
        set
        {
            if (Set(ref _userNicknameOffsetX, value))
            {
                Raise(nameof(UserNicknameOffsetXLabel));
            }
        }
    }

    public double UserNicknameOffsetY
    {
        get => _userNicknameOffsetY;
        set
        {
            if (Set(ref _userNicknameOffsetY, value))
            {
                Raise(nameof(UserNicknameOffsetYLabel));
            }
        }
    }

    public double BubbleRadius
    {
        get => _bubbleRadius;
        set
        {
            if (Set(ref _bubbleRadius, value))
            {
                Raise(nameof(BubbleRadiusLabel));
            }
        }
    }

    public double BubblePaddingX
    {
        get => _bubblePaddingX;
        set
        {
            if (Set(ref _bubblePaddingX, value))
            {
                Raise(nameof(BubblePaddingXLabel));
            }
        }
    }

    public double BubblePaddingY
    {
        get => _bubblePaddingY;
        set
        {
            if (Set(ref _bubblePaddingY, value))
            {
                Raise(nameof(BubblePaddingYLabel));
            }
        }
    }

    public double MessageGap
    {
        get => _messageGap;
        set
        {
            if (Set(ref _messageGap, value))
            {
                Raise(nameof(MessageGapLabel));
            }
        }
    }

    public double AssistantBubbleMaxWidth
    {
        get => _assistantBubbleMaxWidth;
        set
        {
            if (Set(ref _assistantBubbleMaxWidth, value))
            {
                Raise(nameof(AssistantBubbleMaxWidthLabel));
            }
        }
    }

    public bool NicknameVisible
    {
        get => _nicknameVisible;
        set => Set(ref _nicknameVisible, value);
    }

    public string UserBubble
    {
        get => _userBubble;
        set => Set(ref _userBubble, value);
    }

    public string AssistantBubble
    {
        get => _assistantBubble;
        set
        {
            if (Set(ref _assistantBubble, value))
            {
                Raise(nameof(PreviewAssistantBubble));
            }
        }
    }

    public string DarkAssistantText
    {
        get => _darkAssistantText;
        set
        {
            if (Set(ref _darkAssistantText, value))
            {
                Raise(nameof(PreviewAssistantText));
            }
        }
    }

    public string DarkNicknameColor
    {
        get => _darkNicknameColor;
        set
        {
            if (Set(ref _darkNicknameColor, value))
            {
                Raise(nameof(PreviewNicknameColor));
            }
        }
    }

    public string DarkAvatarBackground
    {
        get => _darkAvatarBackground;
        set
        {
            if (Set(ref _darkAvatarBackground, value))
            {
                Raise(nameof(PreviewAvatarBackground));
            }
        }
    }

    public string DarkAvatarBorder
    {
        get => _darkAvatarBorder;
        set
        {
            if (Set(ref _darkAvatarBorder, value))
            {
                Raise(nameof(PreviewAvatarBorder));
            }
        }
    }

    public string LightAssistantBubble
    {
        get => _lightAssistantBubble;
        set
        {
            if (Set(ref _lightAssistantBubble, value))
            {
                Raise(nameof(PreviewAssistantBubble));
            }
        }
    }

    public string LightAssistantText
    {
        get => _lightAssistantText;
        set
        {
            if (Set(ref _lightAssistantText, value))
            {
                Raise(nameof(PreviewAssistantText));
            }
        }
    }

    public string LightNicknameColor
    {
        get => _lightNicknameColor;
        set
        {
            if (Set(ref _lightNicknameColor, value))
            {
                Raise(nameof(PreviewNicknameColor));
            }
        }
    }

    public string LightAvatarBackground
    {
        get => _lightAvatarBackground;
        set
        {
            if (Set(ref _lightAvatarBackground, value))
            {
                Raise(nameof(PreviewAvatarBackground));
            }
        }
    }

    public string LightAvatarBorder
    {
        get => _lightAvatarBorder;
        set
        {
            if (Set(ref _lightAvatarBorder, value))
            {
                Raise(nameof(PreviewAvatarBorder));
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string DiagnosticsText
    {
        get => _diagnosticsText;
        set => Set(ref _diagnosticsText, value);
    }

    public DesktopSessionState SessionState
    {
        get => _sessionState;
        private set
        {
            if (!Set(ref _sessionState, value))
            {
                return;
            }
            Raise(nameof(ConnectionSummary));
            Raise(nameof(IsConnected));
            Raise(nameof(IsSkinEnabled));
            Raise(nameof(IsSkinRequested));
            Raise(nameof(SessionStatus));
            Raise(nameof(AppearanceStatus));
            RaiseHomeActionState();
            RaiseCommandCanExecute();
        }
    }

    public bool IsConnected => SessionState.IsConnected;
    public bool IsSkinEnabled => SessionState.IsSkinEnabled;
    public bool IsSkinRequested => SessionState.IsSkinRequested;
    public bool HasSelectedCandidate => SelectedCandidate is not null;
    public bool ShowDetectAction => SelectedCandidate is null;
    public bool ShowStartAction => SelectedCandidate is not null && !IsConnected;
    public bool ShowEnableAction => IsConnected && !IsSkinRequested;
    public bool ShowDisableAction => IsConnected && IsSkinRequested;
    public bool ShowRestartAction => SelectedCandidate is not null && IsConnected;
    public string ConnectionSummary =>
        SessionState.IsConnected
            ? SessionState.Transport == DesktopDebugTransport.Pipe
                ? LocalizationService.Format(
                    "ConnectionPipeFormat",
                    SessionState.TargetCount)
                : LocalizationService.Format(
                    "ConnectionTcpFormat",
                    SessionState.CdpPort,
                    SessionState.TargetCount)
            : LocalizationService.Get("ConnectionDisconnected");
    public string CalibrationSummary =>
        LocalizationService.Format(
            "CalibrationSummaryFormat",
            _calibration.AssistantTurn is null
                ? LocalizationService.Get("CalibrationNotCalibrated")
                : LocalizationService.Get("CalibrationReady"),
            _calibration.UserTurn is null
                ? LocalizationService.Get("CalibrationNotCalibrated")
                : LocalizationService.Get("CalibrationReady"));
    public string SessionStatus => LocalizeSessionStatus(SessionState.Status);
    public string AppearanceStatus =>
        LocalizationService.Get(
            IsSkinEnabled
                ? "HomeAppearanceActive"
                : IsSkinRequested
                    ? "HomeAppearancePending"
                    : "HomeAppearanceOff");
    public string AvatarSizeLabel =>
        LocalizationService.Format("AvatarSizeDeltaFormat", AvatarSize);
    public string AssistantAvatarOffsetXLabel =>
        LocalizationService.Format("AvatarOffsetXFormat", AssistantAvatarOffsetX);
    public string AssistantAvatarOffsetYLabel =>
        LocalizationService.Format("AvatarOffsetYFormat", AssistantAvatarOffsetY);
    public string UserAvatarOffsetXLabel =>
        LocalizationService.Format("AvatarOffsetXFormat", UserAvatarOffsetX);
    public string UserAvatarOffsetYLabel =>
        LocalizationService.Format("AvatarOffsetYFormat", UserAvatarOffsetY);
    public string AssistantNicknameOffsetXLabel =>
        LocalizationService.Format("NicknameOffsetXFormat", AssistantNicknameOffsetX);
    public string AssistantNicknameOffsetYLabel =>
        LocalizationService.Format("NicknameOffsetYFormat", AssistantNicknameOffsetY);
    public string UserNicknameOffsetXLabel =>
        LocalizationService.Format("NicknameOffsetXFormat", UserNicknameOffsetX);
    public string UserNicknameOffsetYLabel =>
        LocalizationService.Format("NicknameOffsetYFormat", UserNicknameOffsetY);
    public string BubbleRadiusLabel =>
        LocalizationService.Format("BubbleRadiusDeltaFormat", BubbleRadius);
    public string BubblePaddingXLabel =>
        LocalizationService.Format("HorizontalPaddingDeltaFormat", BubblePaddingX);
    public string BubblePaddingYLabel =>
        LocalizationService.Format("VerticalPaddingDeltaFormat", BubblePaddingY);
    public string MessageGapLabel =>
        LocalizationService.Format("MessageGapDeltaFormat", MessageGap);
    public string AssistantBubbleMaxWidthLabel =>
        LocalizationService.Format("AssistantBubbleMaxWidthDeltaFormat", AssistantBubbleMaxWidth);

    public bool WasFirstRun { get; private set; }
    public string VersionLabel => BuildInfo.Version;
    public string CurrentVersionLabel =>
        LocalizationService.Format("UpdateCurrentVersionFormat", BuildInfo.Version);
    public string UpdateStatusText { get; private set; } =
        LocalizationService.Get("UpdateStatusReady");

    public async Task InitializeAsync()
    {
        // Load local state before detection so first-run and recovery messages are accurate.
        var load = await _configStore.LoadAsync().ConfigureAwait(true);
        WasFirstRun = load.WasCreated;
        var config = await MigrateLegacyAvatarsAsync(load.Config).ConfigureAwait(true);
        if (WasFirstRun)
        {
            config = await SeedFirstRunAssistantAvatarAsync(config)
                .ConfigureAwait(true);
        }
        _persistedConfig = config;
        RefreshManagerThemeOptions(config.ManagerThemeMode);
        RefreshBubbleDisplayModeOptions(config.Appearance.BubbleDisplayMode);
        LoadConfig(config);
        var startupRegistrationRecovered =
            await ReconcileStartupRegistrationAsync().ConfigureAwait(true);
        var associationRecovered =
            await ReconcileCodexLaunchAssociationAsync().ConfigureAwait(true);
        await DetectAsync().ConfigureAwait(true);
        SetStatus(
             startupRegistrationRecovered || associationRecovered
                ? "StatusStartupRegistrationRecovered"
                : load.CorruptBackupPath is null
                ? "StatusReady"
                : "StatusRecoveredConfig");
        _initialized = true;
    }

    public Task StartAutomaticallyIfConfiguredAsync() =>
        RunDesktopOperationAsync(() => StartAutomaticallyCoreAsync(force: false));

    public Task StartFromAssociatedLaunchAsync() =>
        RunDesktopOperationAsync(
            () => StartAutomaticallyCoreAsync(force: true),
            queueWhenBusy: true);

    private async Task StartAutomaticallyCoreAsync(bool force)
    {
        try
        {
            await DetectAsync().ConfigureAwait(true);
            var candidate = SelectedCandidate;
            var decision = AutomaticCodexLaunchPolicy.Decide(
                force || _persistedConfig.LaunchCodexOnMycoStart,
                candidate is not null,
                Candidates.Any(candidate => candidate.IsRunning),
                SessionState.IsConnected);
            if (decision is AutomaticCodexLaunchDecision.Disabled or
                AutomaticCodexLaunchDecision.AlreadyControlled)
            {
                return;
            }
            if (decision == AutomaticCodexLaunchDecision.DesktopNotFound)
            {
                SetStatus("StatusAutoStartAppNotFound");
                _logger.Info("auto_start_codex_not_found");
                return;
            }
            if (decision == AutomaticCodexLaunchDecision.AlreadyRunningUncontrolled)
            {
                SetStatus("StatusAutoStartSkippedRunning");
                _logger.Info("auto_start_codex_already_running");
                return;
            }

            var adapter = _adapters.Select(candidate!)
                          ?? throw new NotSupportedException(
                              "No compatible application adapter is available.");
            SetStatus("StatusAutoStartingDesktop");
            await StartControllerWithFallbackAsync(
                candidate!,
                adapter,
                allowInteractiveFallback: false).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            var errorCode = ErrorCodeFactory.Create("AUTO", "START");
            _logger.Error(errorCode, exception);
            SetStatus("StatusAutoStartFailedFormat", errorCode);
        }
    }

    private async Task<AppConfig> MigrateLegacyAvatarsAsync(AppConfig config)
    {
        var assistant = await ImportLegacyAvatarAsync(
            config.Assistant.Avatar).ConfigureAwait(true);
        var user = await ImportLegacyAvatarAsync(config.User.Avatar).ConfigureAwait(true);
        if (string.Equals(
                assistant,
                config.Assistant.Avatar,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                user,
                config.User.Avatar,
                StringComparison.OrdinalIgnoreCase))
        {
            return config;
        }
        var migrated = config with
        {
            Assistant = config.Assistant with { Avatar = assistant },
            User = config.User with { Avatar = user }
        };
        await _configStore.SaveAsync(migrated).ConfigureAwait(true);
        return migrated;
    }

    private async Task<string> ImportLegacyAvatarAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }
        try
        {
            return (await _avatarService.ImportAsync(path).ConfigureAwait(true)).StoredPath;
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or ArgumentException or
                IOException or UnauthorizedAccessException)
        {
            _logger.Error("avatar_migration_rejected", exception);
            return string.Empty;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _themeService.ThemeChanged -= HandleThemeChanged;
        _controller.StateChanged -= HandleStateChanged;
        _controller.RuntimeEventReceived -= HandleRuntimeEvent;
        await _controller.DisposeAsync().ConfigureAwait(false);
        _updateCoordinator.Dispose();
    }

    private async Task DetectAsync()
    {
        var candidates = await _locator.FindCandidatesAsync().ConfigureAwait(true);
        ReplaceCandidates(candidates, candidates.FirstOrDefault());
        if (SelectedCandidate is null)
        {
            SetStatus("StatusAppNotFound");
        }
        else if (SelectedCandidate.IsRunning)
        {
            SetStatus(
                "StatusAppRunningFormat",
                SelectedCandidate.DisplayName,
                SelectedCandidate.Version);
        }
        else
        {
            SetStatus(
                "StatusAppDetectedFormat",
                SelectedCandidate.DisplayName,
                SelectedCandidate.Version);
        }
    }

    private async Task StartAsync(bool explicitRestart)
    {
        await _restartGate.WaitAsync().ConfigureAwait(true);
        try
        {
            await StartCoreAsync(explicitRestart).ConfigureAwait(true);
        }
        finally
        {
            _restartGate.Release();
        }
    }

    private async Task StartCoreAsync(bool explicitRestart)
    {
        _logger.Info(
            explicitRestart ? "restart_requested" : "start_requested",
            new Dictionary<string, object?>
            {
                ["state"] = SessionState.Phase.ToString().ToLowerInvariant(),
                ["connected"] = _controller.State.IsConnected
            });

        // A stale CDP session (renderer already gone) must not delay the restart.
        if (_controller.State.IsConnected)
        {
            SetStatus("StatusDisconnectingForRestart");
            await _controller.DisconnectAsync().ConfigureAwait(true);
        }

        var candidate = SelectedCandidate
                        ?? throw new InvalidOperationException("No Desktop candidate is selected.");
        candidate = await RefreshCandidateAsync(candidate).ConfigureAwait(true);
        if (!candidate.IsRunning && Candidates.Any(current => current.IsRunning))
        {
            SetStatus("StatusAutoStartSkippedRunning");
            _logger.Info("start_codex_skipped_other_running_candidate");
            return;
        }
        // Chromium reads CDP flags only at launch, so an ordinary running instance must restart.
        if (candidate.IsRunning)
        {
            if (!explicitRestart)
            {
                var restart = System.Windows.MessageBox.Show(
                    LocalizationService.Get("RestartPrompt"),
                    LocalizationService.Get("RestartTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (restart != MessageBoxResult.Yes)
                {
                    SetStatus("StatusRestartCancelled");
                    return;
                }
            }
            SetStatus("StatusNormalShutdown");
            var closeResult = await CloseForRestartAsync(candidate).ConfigureAwait(true);
            _logger.Info("desktop_restart_closed", new Dictionary<string, object?>
            {
                ["stage"] = "shutdown",
                ["outcome"] = closeResult.UsedVerifiedForceClose
                    ? "verified_force"
                    : "graceful"
            });

            // Keep the captured installation identity. Re-enumerating the MSIX
            // repository immediately after shutdown can transiently return no
            // candidate even though the verified executable remains available.
            candidate = candidate with
            {
                IsRunning = false,
                WindowTitle = null
            };
            ReplaceCandidates(
                Candidates.Select(current =>
                    ApplicationCandidateResolver.StableKey(current).Equals(
                        ApplicationCandidateResolver.StableKey(candidate),
                        StringComparison.OrdinalIgnoreCase)
                        ? candidate
                        : current).ToArray(),
                candidate);
        }

        var adapter = _adapters.Select(candidate)
                      ?? throw new NotSupportedException(
                          "No compatible application adapter is available.");
        try
        {
            await LaunchWithRetryAsync(candidate, adapter).ConfigureAwait(true);
        }
        catch
        {
            await RefreshAfterLaunchFailureAsync(candidate).ConfigureAwait(true);
            throw;
        }
    }

    private async Task<ApplicationRestartCloseResult> CloseForRestartAsync(
        ApplicationCandidate candidate)
    {
        try
        {
            return await _restartService.CloseForRestartAsync(
                candidate,
                _gracefulShutdownTimeout,
                _forceShutdownTimeout,
                _quiescenceTimeout).ConfigureAwait(true);
        }
        catch (ApplicationRestartException restartException)
        {
            // A closing instance may still hold process resources. Do not abort
            // the whole restart transaction on a shutdown-stage timeout; verify
            // that the old root is actually gone and continue into the launch.
            _logger.Error("desktop_restart_close_stage", restartException);
            var stillRunning = _restartService.IsRootRunning(candidate);
            if (stillRunning)
            {
                throw;
            }
            SetStatus("StatusRestartingDesktop");
            return new ApplicationRestartCloseResult(
                UsedVerifiedForceClose: true,
                Targets: []);
        }
        catch
        {
            throw;
        }
    }

    private async Task LaunchWithRetryAsync(
        ApplicationCandidate candidate,
        IApplicationAdapter adapter)
    {
        const int maximumAttempts = 3;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            SetStatus("StatusStartingDesktopAttemptFormat", attempt, maximumAttempts);
            try
            {
                await StartControllerWithFallbackAsync(
                    candidate,
                    adapter,
                    allowInteractiveFallback: true).ConfigureAwait(true);
                return;
            }
            catch (FileNotFoundException) when (attempt == 1)
            {
                SetStatus("StatusRefreshingDesktopEntry");
                candidate = await RefreshCandidateAsync(candidate).ConfigureAwait(true);
                adapter = _adapters.Select(candidate)
                          ?? throw new NotSupportedException(
                              "No compatible application adapter is available.");
            }
            catch (Exception exception) when (
                attempt < maximumAttempts &&
                exception is (
                    DesktopProcessExitedBeforeReadyException or
                    DesktopRendererNotReadyException))
            {
                _logger.Info("desktop_launch_retry", new Dictionary<string, object?>
                {
                    ["attempt"] = attempt,
                    ["stage"] = "renderer_readiness",
                    ["outcome"] = exception is DesktopProcessExitedBeforeReadyException
                        ? "early_exit"
                        : "renderer_not_ready"
                });
                SetStatus("StatusLaunchRetryFormat", attempt + 1, maximumAttempts);
                // The retry may target a leftover instance from the previous attempt.
                await _restartService.CloseForRestartAsync(
                    candidate,
                    gracefulTimeout: TimeSpan.FromSeconds(5),
                    forceTimeout: TimeSpan.FromSeconds(8),
                    quiescenceTimeout: TimeSpan.FromSeconds(10)).ConfigureAwait(true);
                candidate = candidate with
                {
                    IsRunning = false,
                    WindowTitle = null
                };
                adapter = _adapters.Select(candidate)
                          ?? throw new NotSupportedException(
                              "No compatible application adapter is available.");
            }
        }
    }

    private async Task RefreshAfterLaunchFailureAsync(
        ApplicationCandidate previous)
    {
        try
        {
            var candidates = await _locator.FindCandidatesAsync().ConfigureAwait(true);
            var resolved = ApplicationCandidateResolver.ResolveCurrent(
                previous,
                candidates);
            ReplaceCandidates(candidates, resolved ?? candidates.FirstOrDefault());
        }
        catch (Exception exception)
        {
            // Recovery is best effort; preserve the original actionable failure.
            _logger.Error("desktop_launch_state_refresh_failed", exception);
        }
    }

    private async Task StartControllerWithFallbackAsync(
        ApplicationCandidate candidate,
        IApplicationAdapter adapter,
        bool allowInteractiveFallback)
    {
        try
        {
            await _controller.StartAsync(
                candidate,
                adapter,
                BuildConfig(),
                DesktopDebugTransport.Pipe).ConfigureAwait(true);
        }
        catch (SecureTransportUnavailableException exception)
        {
            if (!allowInteractiveFallback)
            {
                throw;
            }
            var useTcp = System.Windows.MessageBox.Show(
                LocalizationService.Format(
                    "TcpFallbackPromptFormat",
                    exception.ErrorCode),
                LocalizationService.Get("TcpFallbackTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (useTcp != MessageBoxResult.Yes)
            {
                SetStatus("StatusTcpFallbackDeclined");
                return;
            }
            SetStatus("StatusStartingTcpFallback");
            await _controller.StartAsync(
                candidate,
                adapter,
                BuildConfig(),
                DesktopDebugTransport.Tcp).ConfigureAwait(true);
        }
    }

    private async Task<ApplicationCandidate> RefreshCandidateAsync(
        ApplicationCandidate previous)
    {
        var candidates = await _locator.FindCandidatesAsync().ConfigureAwait(true);
        var resolved = ApplicationCandidateResolver.ResolveCurrent(
            previous,
            candidates);
        ReplaceCandidates(candidates, resolved);
        return resolved
               ?? throw new FileNotFoundException(
                   "The selected Desktop installation is no longer registered.");
    }

    private void ReplaceCandidates(
        IEnumerable<ApplicationCandidate> candidates,
        ApplicationCandidate? selected)
    {
        Candidates.Clear();
        foreach (var candidate in candidates)
        {
            Candidates.Add(candidate);
        }
        SelectedCandidate = selected;
    }

    private async Task SaveAndApplyAsync()
    {
        var config = BuildConfig();
        await SaveConfigAsync(config).ConfigureAwait(true);
        RuntimeConfigApplyResult applyResult = new(0, 0, 0);
        if (_controller.State.IsConnected)
        {
            applyResult = await _controller.ApplyConfigAsync(config).ConfigureAwait(true);
        }
        if (applyResult.SessionCount == 0)
        {
            SetStatus("StatusAppearanceSavedNoSessions");
        }
        else if (applyResult.IsFullyApplied)
        {
            SetStatus(
                "StatusAppearanceSavedAndAppliedFormat",
                applyResult.AppliedCount);
        }
        else
        {
            SetStatus(
                "StatusAppearancePartiallyAppliedFormat",
                applyResult.AppliedCount,
                applyResult.SessionCount,
                applyResult.FailedCount);
        }
    }

    private async Task SaveSettingsAsync()
    {
        var executable = Environment.ProcessPath
                         ?? throw new InvalidOperationException(
                             "The MyCO executable path is unavailable.");
        var previousConfig = _persistedConfig;
        var previousRegistration = _startupRegistration.GetStatus(executable);
        var previousAssociation =
            _codexLaunchAssociation.CaptureSnapshot(executable);
        try
        {
            _startupRegistration.SetEnabled(executable, LaunchAtLogin);
            _codexLaunchAssociation.SetEnabled(
                executable,
                AssociateCodexLaunches,
                previousAssociation);
            var config = BuildConfig();
            await SaveConfigAsync(config).ConfigureAwait(true);
            _themeService.ApplyMode(config.ManagerThemeMode);
            SetStatus("StatusSettingsSaved");
        }
        catch
        {
            try
            {
                _startupRegistration.Restore(previousRegistration);
            }
            catch (Exception rollbackException)
            {
                _logger.Error("startup_registration_rollback_failed", rollbackException);
            }
            try
            {
                _codexLaunchAssociation.Restore(previousAssociation);
            }
            catch (Exception rollbackException)
            {
                _logger.Error("codex_association_rollback_failed", rollbackException);
            }
            try
            {
                await SaveConfigAsync(previousConfig).ConfigureAwait(true);
            }
            catch (Exception rollbackException)
            {
                _logger.Error("settings_config_rollback_failed", rollbackException);
                _persistedConfig = previousConfig;
            }
            RefreshManagerThemeOptions(previousConfig.ManagerThemeMode);
            LoadConfig(previousConfig);
            throw;
        }
    }

    private async Task<bool> ReconcileStartupRegistrationAsync()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }
        try
        {
            var status = _startupRegistration.GetStatus(executable);
            if (_persistedConfig.LaunchAtLogin)
            {
                if (!status.MatchesCurrentExecutable)
                {
                    _startupRegistration.SetEnabled(executable, enabled: true);
                }
            }
            else if (status.IsRegistered)
            {
                _startupRegistration.SetEnabled(executable, enabled: false);
            }
            return false;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                UnauthorizedAccessException or
                System.Security.SecurityException or
                IOException)
        {
            _logger.Error("startup_registration_reconcile_failed", exception);
            if (_persistedConfig.LaunchAtLogin)
            {
                _persistedConfig = _persistedConfig with { LaunchAtLogin = false };
                LaunchAtLogin = false;
                await SaveConfigAsync(_persistedConfig).ConfigureAwait(true);
            }
            return true;
        }
    }

    private async Task PickAvatarAsync(bool assistant)
    {
        // Validate before opening the cropper; only confirmed crop bytes are stored.
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.Get(
                assistant
                    ? "ChooseAssistantAvatarTitle"
                    : "ChooseUserAvatarTitle"),
            Filter = LocalizationService.Get("ImageFilter"),
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog() != true)
        {
            return;
        }
        byte[] imageBytes;
        try
        {
            imageBytes = await _avatarService.ReadValidatedAsync(picker.FileName)
                .ConfigureAwait(true);
        }
        catch (ArgumentException exception)
        {
            _logger.Info(
                "avatar_import_rejected",
                new Dictionary<string, object?>
                {
                    ["stage"] = "validation",
                    ["outcome"] = "rejected"
                });
            throw new AvatarImportException(
                AvatarImportFailure.Validation,
                exception);
        }

        AvatarCropWindow cropWindow;
        try
        {
            cropWindow = new AvatarCropWindow(imageBytes)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException or FileFormatException or
                InvalidOperationException or NotSupportedException)
        {
            _logger.Info(
                "avatar_import_rejected",
                new Dictionary<string, object?>
                {
                    ["stage"] = "decode",
                    ["outcome"] = "rejected"
                });
            throw new AvatarImportException(
                AvatarImportFailure.Decode,
                exception);
        }
        if (cropWindow.ShowDialog() != true ||
            cropWindow.CroppedPng is not { Length: > 0 } croppedPng)
        {
            return;
        }
        await using var croppedStream = new MemoryStream(croppedPng, writable: false);
        var imported = await _avatarService.ImportAsync(croppedStream)
            .ConfigureAwait(true);
        if (assistant)
        {
            AssistantAvatar = imported.StoredPath;
        }
        else
        {
            UserAvatar = imported.StoredPath;
        }
        await SaveAndApplyAsync().ConfigureAwait(true);
    }

    private async Task ConfirmAndFactoryResetAsync()
    {
        var confirmation = new ResetConfirmationWindow
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (confirmation.ShowDialog() != true)
        {
            return;
        }

        await RunDesktopOperationAsync(FactoryResetAsync).ConfigureAwait(true);
    }

    private async Task FactoryResetAsync()
    {
        var executable = Environment.ProcessPath
                         ?? throw new InvalidOperationException(
                             "The MyCO executable path is unavailable.");
        var previousConfig = _persistedConfig;
        var previousWasFirstRun = WasFirstRun;
        var previousRegistration = _startupRegistration.GetStatus(executable);
        var previousAssociation =
            _codexLaunchAssociation.CaptureSnapshot(executable);
        var restoreSkin = SessionState.IsConnected && SessionState.IsSkinRequested;
        FactoryResetTransaction? transaction = null;

        try
        {
            if (SessionState.IsConnected)
            {
                await _controller.DisableSkinAsync().ConfigureAwait(true);
            }

            _startupRegistration.SetEnabled(executable, enabled: false);
            _codexLaunchAssociation.SetEnabled(
                executable,
                enabled: false,
                previousAssociation);
            transaction = _factoryResetService.Stage();

            var load = await _configStore.LoadAsync().ConfigureAwait(true);
            var defaults = await SeedFirstRunAssistantAvatarAsync(
                    load.Config,
                    required: true)
                .ConfigureAwait(true);
            // This is process-independent boot-session state, not a user
            // appearance setting. Preserve it so Reset cannot show a second
            // balloon during the same Windows boot.
            defaults = defaults with
            {
                TrayMinimizeNotificationBootId =
                    previousConfig.TrayMinimizeNotificationBootId
            };
            await SaveConfigAsync(defaults).ConfigureAwait(true);
            _persistedConfig = defaults;
            WasFirstRun = true;
            RefreshManagerThemeOptions(defaults.ManagerThemeMode);
            RefreshBubbleDisplayModeOptions(defaults.Appearance.BubbleDisplayMode);
            LoadConfig(defaults);
            _themeService.ApplyMode(defaults.ManagerThemeMode);
            if (SessionState.IsConnected)
            {
                await _controller.ApplyConfigAsync(defaults).ConfigureAwait(true);
            }

            transaction.Commit();
            transaction = null;
            SetStatus("StatusFactoryResetComplete");

            try
            {
                new OnboardingWindow(this)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                }.ShowDialog();
            }
            catch (InvalidOperationException exception)
            {
                // Reset has committed; a presentation failure must not undo valid data.
                _logger.Error("factory_reset_onboarding_failed", exception);
            }
        }
        catch
        {
            try
            {
                transaction?.Rollback();
                transaction = null;
            }
            catch (Exception rollbackException)
            {
                _logger.Error("factory_reset_data_rollback_failed", rollbackException);
            }

            try
            {
                _startupRegistration.Restore(previousRegistration);
            }
            catch (Exception rollbackException)
            {
                _logger.Error("factory_reset_startup_rollback_failed", rollbackException);
            }
            try
            {
                _codexLaunchAssociation.Restore(previousAssociation);
            }
            catch (Exception rollbackException)
            {
                _logger.Error("factory_reset_association_rollback_failed", rollbackException);
            }

            _persistedConfig = previousConfig;
            WasFirstRun = previousWasFirstRun;
            RefreshManagerThemeOptions(previousConfig.ManagerThemeMode);
            RefreshBubbleDisplayModeOptions(previousConfig.Appearance.BubbleDisplayMode);
            LoadConfig(previousConfig);
            _themeService.ApplyMode(previousConfig.ManagerThemeMode);
            if (SessionState.IsConnected)
            {
                try
                {
                    await _controller.ApplyConfigAsync(previousConfig).ConfigureAwait(true);
                    if (restoreSkin)
                    {
                        await _controller.EnableSkinAsync().ConfigureAwait(true);
                    }
                }
                catch (Exception rollbackException)
                {
                    _logger.Error("factory_reset_runtime_rollback_failed", rollbackException);
                }
            }
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                try
                {
                    transaction.Dispose();
                }
                catch (Exception rollbackException)
                {
                    _logger.Error("factory_reset_dispose_rollback_failed", rollbackException);
                }
            }
        }
    }

    private async Task<AppConfig> SeedFirstRunAssistantAvatarAsync(
        AppConfig config,
        bool required = false)
    {
        if (!string.IsNullOrWhiteSpace(config.Assistant.Avatar))
        {
            return config;
        }

        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/MyCO-logo.png", UriKind.Absolute));
            if (resource?.Stream is null)
            {
                throw new FileNotFoundException("The packaged MyCO logo was not found.");
            }
            using (resource.Stream)
            {
                var imported = await _avatarService.ImportAsync(resource.Stream)
                    .ConfigureAwait(true);
                var seeded = config with
                {
                    Assistant = config.Assistant with
                    {
                        Avatar = imported.StoredPath
                    }
                };
                await _configStore.SaveAsync(seeded).ConfigureAwait(true);
                return seeded;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or FileNotFoundException or
                IOException or InvalidOperationException or
                UnauthorizedAccessException or NotSupportedException or
                System.Security.SecurityException)
        {
            _logger.Error("default_assistant_avatar_seed_failed", exception);
            if (required)
            {
                throw;
            }
            return config;
        }
    }

    private async Task RefreshDiagnosticsAsync()
    {
        // Export technical metadata only; runtime diagnostics never contain conversation text.
        var runtime = _controller.State.IsConnected
            ? await _controller.GetDiagnosticsAsync().ConfigureAwait(true)
            : [];
        DiagnosticsText = JsonSerializer.Serialize(new
        {
            managerVersion = BuildInfo.Version,
            protocolVersion = BuildInfo.ProtocolVersion,
            operatingSystem = Environment.OSVersion.VersionString,
            desktopCandidates = Candidates.Select(candidate => new
            {
                candidate.DisplayName,
                candidate.Version,
                candidate.Architecture,
                candidate.PackageIdentity,
                candidate.IsRunning,
                candidate.Score
            }),
            session = SessionState,
            calibration = new
            {
                assistant = _calibration.AssistantTurn is not null,
                user = _calibration.UserTurn is not null
            },
            runtime
        }, JsonOptions);
        _diagnosticsGenerated = true;
        CurrentPage = ManagerPage.Diagnostics;
        SetStatus("StatusDiagnosticsRefreshed");
    }

    private void ResetAppearance()
    {
        var defaults = AppConfig.Default;
        LoadConfig(
            defaults with
            {
                Language = SelectedLanguage.Code,
                ManagerThemeMode =
                    SelectedManagerThemeOption?.Mode ?? ManagerThemeMode.System,
                LaunchAtLogin = LaunchAtLogin,
                LaunchCodexOnMycoStart = LaunchCodexOnMycoStart,
                AssociateCodexLaunches = AssociateCodexLaunches,
                TrayMinimizeNotificationBootId = _trayMinimizeNotificationBootId,
                Calibration = _calibration
            });
        SetStatus("StatusDefaultsRestored");
    }

    private void OpenConfigFolder()
    {
        _paths.EnsureDirectories();
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            ArgumentList = { _paths.BaseDirectory },
            UseShellExecute = true
        });
    }

    private AppConfig BuildConfig()
    {
        var geometry = new AppearanceGeometryDeltas
        {
            AvatarSizeDelta = (int)Math.Round(AvatarSize),
            AssistantAvatarOffsetXDelta = (int)Math.Round(AssistantAvatarOffsetX),
            AssistantAvatarOffsetYDelta = (int)Math.Round(AssistantAvatarOffsetY),
            UserAvatarOffsetXDelta = (int)Math.Round(UserAvatarOffsetX),
            UserAvatarOffsetYDelta = (int)Math.Round(UserAvatarOffsetY),
            AssistantNicknameOffsetXDelta = (int)Math.Round(AssistantNicknameOffsetX),
            AssistantNicknameOffsetYDelta = (int)Math.Round(AssistantNicknameOffsetY),
            UserNicknameOffsetXDelta = (int)Math.Round(UserNicknameOffsetX),
            UserNicknameOffsetYDelta = (int)Math.Round(UserNicknameOffsetY),
            BubbleRadiusDelta = (int)Math.Round(BubbleRadius),
            BubblePaddingXDelta = (int)Math.Round(BubblePaddingX),
            BubblePaddingYDelta = (int)Math.Round(BubblePaddingY),
            MessageGapDelta = (int)Math.Round(MessageGap),
            AssistantBubbleMaxWidthDelta = (int)Math.Round(AssistantBubbleMaxWidth)
        };
        var effective = AppearanceGeometryResolver.Resolve(geometry);
        return new AppConfig
        {
            Language = SelectedLanguage.Code,
            ManagerThemeMode =
                SelectedManagerThemeOption?.Mode ?? ManagerThemeMode.System,
            LaunchAtLogin = LaunchAtLogin,
            LaunchCodexOnMycoStart = LaunchCodexOnMycoStart,
            AssociateCodexLaunches = AssociateCodexLaunches,
            TrayMinimizeNotificationBootId = _trayMinimizeNotificationBootId,
            Assistant = new PersonConfig
            {
                Name = NicknameValidator.Normalize(AssistantName),
                Avatar = AssistantAvatar
            },
            User = new PersonConfig
            {
                Name = NicknameValidator.Normalize(UserName),
                Avatar = UserAvatar
            },
            Appearance = new AppearanceConfig
            {
                Preset = "ReferenceDark",
                BubbleDisplayMode =
                    SelectedBubbleDisplayModeOption?.Mode ??
                    BubbleDisplayMode.Automatic,
                GeometryBaselineVersion = AppearanceGeometryResolver.BaselineVersion,
                Geometry = geometry,
                AvatarSize = effective.AvatarSize,
                AssistantAvatarOffsetX = effective.AssistantAvatarOffsetX,
                AssistantAvatarOffsetY = effective.AssistantAvatarOffsetY,
                UserAvatarOffsetX = effective.UserAvatarOffsetX,
                UserAvatarOffsetY = effective.UserAvatarOffsetY,
                AssistantNicknameOffsetX = effective.AssistantNicknameOffsetX,
                AssistantNicknameOffsetY = effective.AssistantNicknameOffsetY,
                UserNicknameOffsetX = effective.UserNicknameOffsetX,
                UserNicknameOffsetY = effective.UserNicknameOffsetY,
                BubbleRadius = effective.BubbleRadius,
                BubblePaddingX = effective.BubblePaddingX,
                BubblePaddingY = effective.BubblePaddingY,
                NicknameVisible = NicknameVisible,
                MessageGap = effective.MessageGap,
                AssistantBubbleMaxWidth = effective.AssistantBubbleMaxWidth,
                UserBubble = _persistedConfig.Appearance.UserBubble,
                UserText = _persistedConfig.Appearance.UserText,
                DarkBubblePalette = new BubblePalette
                {
                    AssistantBubble = AssistantBubble,
                    AssistantText = DarkAssistantText,
                    NicknameColor = DarkNicknameColor,
                    AvatarBackground = DarkAvatarBackground,
                    AvatarBorder = DarkAvatarBorder
                },
                LightBubblePalette = new BubblePalette
                {
                    AssistantBubble = LightAssistantBubble,
                    AssistantText = LightAssistantText,
                    NicknameColor = LightNicknameColor,
                    AvatarBackground = LightAvatarBackground,
                    AvatarBorder = LightAvatarBorder
                }
            },
            Calibration = _calibration
        };
    }

    private void LoadConfig(AppConfig config)
    {
        // Assign through properties so every bound preview label refreshes consistently.
        var language = LocalizationService.SupportedLanguages.First(
            option => string.Equals(
                option.Code,
                LanguageCodes.Normalize(config.Language),
                StringComparison.OrdinalIgnoreCase));
        _selectedLanguage = language;
        LocalizationService.ApplyLanguage(language.Code);
        Raise(nameof(SelectedLanguage));
        SelectedManagerThemeOption = ManagerThemeOptions.First(
            option => option.Mode == config.ManagerThemeMode);
        SelectedBubbleDisplayModeOption = BubbleDisplayModeOptions.First(
            option => option.Mode == config.Appearance.BubbleDisplayMode);
        LaunchAtLogin = config.LaunchAtLogin;
        LaunchCodexOnMycoStart = config.LaunchCodexOnMycoStart;
        AssociateCodexLaunches = config.AssociateCodexLaunches;
        _trayMinimizeNotificationBootId = config.TrayMinimizeNotificationBootId;
        AssistantName = config.Assistant.Name;
        UserName = config.User.Name;
        AssistantAvatar = config.Assistant.Avatar;
        UserAvatar = config.User.Avatar;
        var geometry = config.Appearance.Geometry.IsZero
            ? AppearanceGeometryResolver.FromAbsolute(config.Appearance)
            : config.Appearance.Geometry;
        AvatarSize = geometry.AvatarSizeDelta;
        AssistantAvatarOffsetX = geometry.AssistantAvatarOffsetXDelta;
        AssistantAvatarOffsetY = geometry.AssistantAvatarOffsetYDelta;
        UserAvatarOffsetX = geometry.UserAvatarOffsetXDelta;
        UserAvatarOffsetY = geometry.UserAvatarOffsetYDelta;
        AssistantNicknameOffsetX = geometry.AssistantNicknameOffsetXDelta;
        AssistantNicknameOffsetY = geometry.AssistantNicknameOffsetYDelta;
        UserNicknameOffsetX = geometry.UserNicknameOffsetXDelta;
        UserNicknameOffsetY = geometry.UserNicknameOffsetYDelta;
        BubbleRadius = geometry.BubbleRadiusDelta;
        BubblePaddingX = geometry.BubblePaddingXDelta;
        BubblePaddingY = geometry.BubblePaddingYDelta;
        MessageGap = geometry.MessageGapDelta;
        AssistantBubbleMaxWidth = geometry.AssistantBubbleMaxWidthDelta;
        NicknameVisible = config.Appearance.NicknameVisible;
        AssistantBubble = config.Appearance.DarkBubblePalette.AssistantBubble;
        DarkAssistantText = config.Appearance.DarkBubblePalette.AssistantText;
        DarkNicknameColor = config.Appearance.DarkBubblePalette.NicknameColor;
        DarkAvatarBackground = config.Appearance.DarkBubblePalette.AvatarBackground;
        DarkAvatarBorder = config.Appearance.DarkBubblePalette.AvatarBorder;
        LightAssistantBubble = config.Appearance.LightBubblePalette.AssistantBubble;
        LightAssistantText = config.Appearance.LightBubblePalette.AssistantText;
        LightNicknameColor = config.Appearance.LightBubblePalette.NicknameColor;
        LightAvatarBackground = config.Appearance.LightBubblePalette.AvatarBackground;
        LightAvatarBorder = config.Appearance.LightBubblePalette.AvatarBorder;
        _calibration = config.Calibration;
        RefreshLocalizedProperties();
    }

    private bool CanDetect() =>
        Volatile.Read(ref _desktopOperationInProgress) == 0 &&
        SessionState.Phase is not (
            DesktopSessionPhase.Starting or DesktopSessionPhase.Stopping);

    private bool CanStart() =>
        Volatile.Read(ref _desktopOperationInProgress) == 0 &&
        SelectedCandidate is not null &&
        SessionState.Phase is DesktopSessionPhase.Disconnected or
            DesktopSessionPhase.Faulted;

    private bool CanRestart() =>
        Volatile.Read(ref _desktopOperationInProgress) == 0 &&
        SelectedCandidate is not null &&
        SessionState.Phase is not (
            DesktopSessionPhase.Starting or DesktopSessionPhase.Stopping);

    private bool CanCalibrate() =>
        CanUseDesktopSession() &&
        SessionState.IsConnected &&
        SessionState.IsSkinEnabled;

    private bool CanUseDesktopSession() =>
        Volatile.Read(ref _desktopOperationInProgress) == 0;

    private void RaiseCommandCanExecute()
    {
        foreach (var command in new[]
                 {
                     DetectCommand,
                     StartCommand,
                     RestartCommand,
                     SaveCommand,
                     EnableCommand,
                     DisableCommand,
                     PickAssistantAvatarCommand,
                     PickUserAvatarCommand,
                     CalibrateAssistantCommand,
                      CalibrateUserCommand,
                      RefreshDiagnosticsCommand,
                      FactoryResetCommand,
                      CheckForUpdatesCommand
                 })
        {
            if (command is AsyncRelayCommand asyncCommand)
            {
                asyncCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task GuardAsync(Func<Task> operation, string contextKey)
    {
        // UI commands share one localized error boundary instead of crashing the dispatcher.
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            var context = LocalizationService.Get(contextKey);
            var errorCode = ErrorCodeFactory.Create("UI", contextKey);
            _logger.Error(errorCode, exception);
            var detail = exception switch
            {
                DesktopProcessExitedBeforeReadyException =>
                    LocalizationService.Get("StartExitedBeforeReady"),
                DesktopRendererNotReadyException =>
                    LocalizationService.Get("StartRendererNotReady"),
                ApplicationRestartException restartException =>
                    LocalizationService.Get(restartException.Stage switch
                    {
                        ApplicationRestartStage.IdentityValidation =>
                            "RestartIdentityUnsafe",
                        ApplicationRestartStage.VerifiedForceClose =>
                            "RestartForceCloseFailed",
                        ApplicationRestartStage.ProcessQuiescence =>
                            "StartShutdownNotReady",
                        _ => "RestartShutdownFailed"
                    }),
                AvatarImportException avatarException =>
                    LocalizationService.Get(
                        avatarException.Failure == AvatarImportFailure.Validation
                            ? "AvatarValidationRejected"
                            : "AvatarDecodeRejected"),
                TimeoutException =>
                    LocalizationService.Get("StartShutdownNotReady"),
                _ => null
            };
            SetStatusText(detail is null
                ? LocalizationService.Format("OperationErrorFormat", context, errorCode)
                : LocalizationService.Format(
                    "OperationErrorDetailedFormat",
                    context,
                    detail,
                    errorCode));
            System.Windows.MessageBox.Show(
                detail is null
                    ? LocalizationService.Format("UnhandledErrorFormat", errorCode)
                    : LocalizationService.Format(
                        "OperationErrorDetailedDialogFormat",
                        detail,
                        errorCode),
                context,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void HandleStateChanged(object? sender, DesktopSessionState state)
    {
        // CDP monitoring runs off the UI thread; WPF-bound properties must update on Dispatcher.
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            SessionState = state;
            SetStatusFromSession(state.Status);
        });
    }

    private void HandleRuntimeEvent(object? sender, RuntimeHostEvent hostEvent)
    {
        if (hostEvent.Type != "calibrationResult")
        {
            return;
        }
        // Calibration originates in the renderer and is persisted back on the UI thread.
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var role = hostEvent.Payload.GetProperty("role").GetString();
                if (role is not ("assistant" or "user"))
                {
                    throw new InvalidOperationException(
                        "Calibration role is not recognized.");
                }
                var signature = hostEvent.Payload.GetProperty("signature")
                    .Deserialize<ElementSignature>(JsonOptions);
                if (signature is null ||
                    signature.SchemaVersion != BuildInfo.CalibrationSchemaVersion)
                {
                    throw new InvalidOperationException(
                        "Calibration signature is not supported.");
                }
                var normalized = ElementSignatureValidator.Normalize(signature);
                var candidate = role == "assistant"
                    ? _calibration with { AssistantTurn = normalized }
                    : _calibration with { UserTurn = normalized };
                if (!ElementSignatureValidator.AreDistinctRoles(
                        candidate.UserTurn,
                        candidate.AssistantTurn))
                {
                    throw new InvalidOperationException(
                        "Calibration roles are structurally ambiguous.");
                }
                _calibration = candidate;
                Raise(nameof(CalibrationSummary));
                var config = BuildConfig();
                await SaveConfigAsync(config).ConfigureAwait(true);
                if (_controller.State.IsConnected)
                {
                    await _controller.ApplyConfigAsync(config).ConfigureAwait(true);
                }
                SetStatus(
                    "StatusCalibrationSavedFormat",
                    LocalizationService.Get(
                        role == "assistant" ? "RoleAssistant" : "RoleUser"));
            }
            catch (Exception exception)
            {
                var errorCode = ErrorCodeFactory.Create("CAL", "REJECTED");
                _logger.Error(errorCode, exception);
                SetStatus("StatusCalibrationRejectedFormat", errorCode);
            }
        });
    }

    private async Task SaveConfigAsync(AppConfig config)
    {
        await _configSaveGate.WaitAsync().ConfigureAwait(true);
        try
        {
            await _configStore.SaveAsync(config).ConfigureAwait(true);
            _persistedConfig = config;
        }
        finally
        {
            _configSaveGate.Release();
        }
    }

    private async Task<bool> ReconcileCodexLaunchAssociationAsync()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }
        try
        {
            var status = _codexLaunchAssociation.GetStatus(executable);
            if (_persistedConfig.AssociateCodexLaunches)
            {
                if (!status.IsEnabled)
                {
                    _codexLaunchAssociation.SetEnabled(executable, enabled: true);
                }
            }
            else if (status.IsEnabled)
            {
                _codexLaunchAssociation.SetEnabled(executable, enabled: false);
            }
            return false;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                UnauthorizedAccessException or
                System.Security.SecurityException or
                IOException)
        {
            _logger.Error("codex_association_reconcile_failed", exception);
            if (_persistedConfig.AssociateCodexLaunches)
            {
                _persistedConfig = _persistedConfig with
                {
                    AssociateCodexLaunches = false
                };
                AssociateCodexLaunches = false;
                await SaveConfigAsync(_persistedConfig).ConfigureAwait(true);
            }
            return true;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        SetUpdateStatus("UpdateStatusChecking");
        var result = await _updateCoordinator.CheckLatestAsync().ConfigureAwait(true);
        switch (result.Outcome)
        {
            case UpdateCheckOutcome.UpToDate:
                SetUpdateStatus("UpdateStatusUpToDate");
                return;
            case UpdateCheckOutcome.Offline:
                SetUpdateStatus("UpdateStatusOffline");
                return;
            case UpdateCheckOutcome.Timeout:
                SetUpdateStatus("UpdateStatusTimeout");
                return;
            case UpdateCheckOutcome.RateLimited:
                SetUpdateStatus("UpdateStatusRateLimited");
                return;
            case UpdateCheckOutcome.InvalidFormat:
                SetUpdateStatus("UpdateStatusInvalid");
                return;
            case UpdateCheckOutcome.Available:
                break;
            default:
                SetUpdateStatus("UpdateStatusInvalid");
                return;
        }

        if (result.Release is null)
        {
            SetUpdateStatus("UpdateStatusInvalid");
            return;
        }
        SetUpdateStatus(
            "UpdateStatusAvailableFormat",
            result.Release.Version.ToString());
        var dialog = new UpdateAvailableWindow(result.Release)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dialog.ShowDialog() != true || !dialog.Confirmed)
        {
            return;
        }

        SetUpdateStatus("UpdateStatusDownloading");
        try
        {
            var prepared = await _updateCoordinator.PrepareAsync(result.Release)
                .ConfigureAwait(true);
            _ = _updateCoordinator.Launch(prepared);
            if (System.Windows.Application.Current?.MainWindow is MainWindow window)
            {
                // The updater waits for this exact process identity, then starts only
                // the new MyCO executable. It never closes or restarts Codex.
                window.RequestExit();
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or
                System.Security.SecurityException or
                DirectoryNotFoundException)
        {
            _logger.Error("update_permission_failed", exception);
            SetUpdateStatus("UpdateStatusPermission");
        }
        catch (Exception exception)
        {
            _logger.Error("update_install_failed", exception);
            SetUpdateStatus("UpdateStatusFailed");
        }
    }

    private async Task PersistTrayMinimizeNotificationBootIdAsync()
    {
        try
        {
            await _configSaveGate.WaitAsync().ConfigureAwait(true);
            try
            {
                var config = _persistedConfig with
                {
                    TrayMinimizeNotificationBootId =
                        _trayMinimizeNotificationBootId
                };
                await _configStore.SaveAsync(config).ConfigureAwait(true);
                _persistedConfig = config;
            }
            finally
            {
                _configSaveGate.Release();
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            _logger.Info("tray_notification_state_save_failed");
        }
    }

    private async Task PersistLanguageAsync(string language)
    {
        try
        {
            AppConfig config;
            await _configSaveGate.WaitAsync().ConfigureAwait(true);
            try
            {
                config = _persistedConfig with
                {
                    Language = LanguageCodes.Normalize(language)
                };
                await _configStore.SaveAsync(config).ConfigureAwait(true);
                _persistedConfig = config;
            }
            finally
            {
                _configSaveGate.Release();
            }
            if (_controller.State.IsConnected)
            {
                await _controller.ApplyConfigAsync(config).ConfigureAwait(true);
            }
            SetStatus("StatusLanguageSaved");
        }
        catch (Exception exception)
        {
            var errorCode = ErrorCodeFactory.Create("CONFIG", "LANGUAGE");
            _logger.Error(errorCode, exception);
            SetStatusText(
                LocalizationService.Format(
                    "OperationErrorFormat",
                    LocalizationService.Get("ErrorSaveLanguage"),
                    errorCode));
        }
    }

    private void RefreshLocalizedProperties()
    {
        RefreshCodexPreviewThemeOptions(
            SelectedCodexPreviewThemeOption?.Theme ??
                ToPreviewTheme(_themeService.EffectiveTheme),
            _previewThemeFollowsManager);
        RefreshManagerThemeOptions(
            SelectedManagerThemeOption?.Mode ?? ManagerThemeMode.System);
        RefreshBubbleDisplayModeOptions(
            SelectedBubbleDisplayModeOption?.Mode ?? BubbleDisplayMode.Automatic);
        Raise(nameof(ConnectionSummary));
        Raise(nameof(CalibrationSummary));
        Raise(nameof(SessionStatus));
        Raise(nameof(AppearanceStatus));
        Raise(nameof(AvatarSizeLabel));
        Raise(nameof(AssistantAvatarOffsetXLabel));
        Raise(nameof(AssistantAvatarOffsetYLabel));
        Raise(nameof(UserAvatarOffsetXLabel));
        Raise(nameof(UserAvatarOffsetYLabel));
        Raise(nameof(AssistantNicknameOffsetXLabel));
        Raise(nameof(AssistantNicknameOffsetYLabel));
        Raise(nameof(UserNicknameOffsetXLabel));
        Raise(nameof(UserNicknameOffsetYLabel));
        Raise(nameof(BubbleRadiusLabel));
        Raise(nameof(BubblePaddingXLabel));
        Raise(nameof(BubblePaddingYLabel));
        Raise(nameof(MessageGapLabel));
        Raise(nameof(AssistantBubbleMaxWidthLabel));
        Raise(nameof(CurrentVersionLabel));
        UpdateStatusText = LocalizationService.Format(
            _updateStatusKey,
            _updateStatusArguments);
        Raise(nameof(UpdateStatusText));
        if (_statusKey is not null)
        {
            Status = LocalizationService.Format(_statusKey, _statusArguments);
        }
        if (!_diagnosticsGenerated)
        {
            DiagnosticsText = LocalizationService.Get("DiagnosticsNotRefreshed");
        }
    }

    private void RaiseHomeActionState()
    {
        Raise(nameof(HasSelectedCandidate));
        Raise(nameof(ShowDetectAction));
        Raise(nameof(ShowStartAction));
        Raise(nameof(ShowEnableAction));
        Raise(nameof(ShowDisableAction));
        Raise(nameof(ShowRestartAction));
    }

    private async Task RunDesktopOperationAsync(
        Func<Task> operation,
        bool queueWhenBusy = false)
    {
        if (Interlocked.CompareExchange(
                ref _desktopOperationInProgress,
                1,
                0) != 0)
        {
            if (queueWhenBusy)
            {
                Interlocked.Exchange(ref _associatedLaunchQueued, 1);
            }
            return;
        }
        RaiseCommandCanExecute();
        try
        {
            await operation().ConfigureAwait(true);
        }
        finally
        {
            Volatile.Write(ref _desktopOperationInProgress, 0);
            RaiseCommandCanExecute();
            if (Interlocked.Exchange(ref _associatedLaunchQueued, 0) != 0)
            {
                await RunDesktopOperationAsync(
                    () => StartAutomaticallyCoreAsync(force: true),
                    queueWhenBusy: true).ConfigureAwait(true);
            }
        }
    }

    private void RefreshManagerThemeOptions(ManagerThemeMode selectedMode)
    {
        ManagerThemeOptions.Clear();
        ManagerThemeOptions.Add(
            new ManagerThemeOption(
                ManagerThemeMode.Dark,
                LocalizationService.Get("ManagerThemeDark")));
        ManagerThemeOptions.Add(
            new ManagerThemeOption(
                ManagerThemeMode.Light,
                LocalizationService.Get("ManagerThemeLight")));
        ManagerThemeOptions.Add(
            new ManagerThemeOption(
                ManagerThemeMode.System,
                LocalizationService.Get("ManagerThemeSystem")));
        SelectedManagerThemeOption = ManagerThemeOptions.First(
            option => option.Mode == selectedMode);
    }

    private void RefreshCodexPreviewThemeOptions(
        CodexPreviewTheme selectedTheme,
        bool? followsManager = null)
    {
        CodexPreviewThemeOptions.Clear();
        CodexPreviewThemeOptions.Add(
            new CodexPreviewThemeOption(
                CodexPreviewTheme.Dark,
                LocalizationService.Get("PreviewModeDark")));
        CodexPreviewThemeOptions.Add(
            new CodexPreviewThemeOption(
                CodexPreviewTheme.Light,
                LocalizationService.Get("PreviewModeLight")));
        _settingPreviewTheme = true;
        try
        {
            SelectedCodexPreviewThemeOption = CodexPreviewThemeOptions.First(
                option => option.Theme == selectedTheme);
        }
        finally
        {
            _settingPreviewTheme = false;
        }
        if (followsManager.HasValue)
        {
            _previewThemeFollowsManager = followsManager.Value;
        }
    }

    private void HandleThemeChanged(object? sender, EventArgs eventArgs)
    {
        if (_previewThemeFollowsManager)
        {
            RefreshCodexPreviewThemeOptions(
                ToPreviewTheme(_themeService.EffectiveTheme),
                followsManager: true);
        }
    }

    private static CodexPreviewTheme ToPreviewTheme(
        EffectiveManagerTheme theme) =>
        theme == EffectiveManagerTheme.Light
            ? CodexPreviewTheme.Light
            : CodexPreviewTheme.Dark;

    private void RaisePreviewPalette()
    {
        Raise(nameof(IsDarkCodexPreview));
        Raise(nameof(PreviewBackground));
        Raise(nameof(PreviewBorder));
        Raise(nameof(PreviewAssistantBubble));
        Raise(nameof(PreviewAssistantText));
        Raise(nameof(PreviewNicknameColor));
        Raise(nameof(PreviewAvatarBackground));
        Raise(nameof(PreviewAvatarBorder));
        Raise(nameof(PreviewUserBubble));
        Raise(nameof(PreviewUserText));
    }

    private void RefreshBubbleDisplayModeOptions(BubbleDisplayMode selectedMode)
    {
        BubbleDisplayModeOptions.Clear();
        BubbleDisplayModeOptions.Add(
            new BubbleDisplayModeOption(
                BubbleDisplayMode.Automatic,
                LocalizationService.Get("BubbleDisplayAutomatic")));
        BubbleDisplayModeOptions.Add(
            new BubbleDisplayModeOption(
                BubbleDisplayMode.Whole,
                LocalizationService.Get("BubbleDisplayWhole")));
        SelectedBubbleDisplayModeOption = BubbleDisplayModeOptions.First(
            option => option.Mode == selectedMode);
    }

    private void SetStatus(string key, params object?[] arguments)
    {
        _statusKey = key;
        _statusArguments = arguments;
        Status = LocalizationService.Format(key, arguments);
    }

    private void SetStatusText(string text)
    {
        _statusKey = null;
        _statusArguments = [];
        Status = text;
    }

    private void SetUpdateStatus(string key, params object?[] arguments)
    {
        _updateStatusKey = key;
        _updateStatusArguments = arguments;
        UpdateStatusText = LocalizationService.Format(key, arguments);
        Raise(nameof(UpdateStatusText));
    }

    private void SetStatusFromSession(string status)
    {
        if (status.StartsWith("Select a assistant", StringComparison.Ordinal))
        {
            SetStatus(
                "StatusSelectTurnFormat",
                LocalizationService.Get("RoleAssistant"));
            return;
        }
        if (status.StartsWith("Select a user", StringComparison.Ordinal))
        {
            SetStatus(
                "StatusSelectTurnFormat",
                LocalizationService.Get("RoleUser"));
            return;
        }

        var key = status switch
        {
            "Not connected" => "ConnectionDisconnected",
            "Skin disabled" => "StatusSkinDisabled",
            "Appearance applied" => "StatusAppearanceApplied",
            "Disconnected" => "StatusDisconnected",
            "Renderer reconnect pending" => "StatusReconnectPending",
            "Waiting for Codex renderer" => "StatusWaitingForRenderer",
            "Skin active" => "StatusSkinActive",
            "Runtime ready: waiting for conversation" => "StatusRuntimeWaiting",
            "Compatibility degraded: no decorated turns" =>
                "StatusCompatibilityDegraded",
            "Safe mode: no compatible renderer" => "StatusSafeMode",
            _ => null
        };
        if (key is null)
        {
            SetStatusText(status);
        }
        else
        {
            SetStatus(key);
        }
    }

    private static string LocalizeSessionStatus(string status)
    {
        if (status.StartsWith("Select a assistant", StringComparison.Ordinal))
        {
            return LocalizationService.Format(
                "StatusSelectTurnFormat",
                LocalizationService.Get("RoleAssistant"));
        }
        if (status.StartsWith("Select a user", StringComparison.Ordinal))
        {
            return LocalizationService.Format(
                "StatusSelectTurnFormat",
                LocalizationService.Get("RoleUser"));
        }

        var key = status switch
        {
            "Not connected" => "ConnectionDisconnected",
            "Skin disabled" => "StatusSkinDisabled",
            "Appearance applied" => "StatusAppearanceApplied",
            "Disconnected" => "StatusDisconnected",
            "Renderer reconnect pending" => "StatusReconnectPending",
            "Waiting for Codex renderer" => "StatusWaitingForRenderer",
            "Skin active" => "StatusSkinActive",
            "Runtime ready: waiting for conversation" => "StatusRuntimeWaiting",
            "Compatibility degraded: no decorated turns" =>
                "StatusCompatibilityDegraded",
            "Safe mode: no compatible renderer" => "StatusSafeMode",
            _ => null
        };
        return key is null ? status : LocalizationService.Get(key);
    }
}
