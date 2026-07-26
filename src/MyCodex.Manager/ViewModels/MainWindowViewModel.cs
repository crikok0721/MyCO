using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using MyCodex.Applications;
using MyCodex.Avatars;
using MyCodex.Compatibility;
using MyCodex.Configuration;
using MyCodex.Injection;
using MyCodex.Manager.Localization;
using MyCodex.Manager.Resources;

namespace MyCodex.Manager.ViewModels;

public enum ManagerPage
{
    Appearance,
    Calibration,
    Diagnostics,
    About
}

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly ConfigPaths _paths = new();
    private readonly ConfigStore _configStore;
    private readonly AvatarService _avatarService;
    private readonly WindowsApplicationLocator _locator = new();
    private readonly ApplicationAdapterCatalog _adapters = new();
    private readonly ApplicationRestartService _restartService = new();
    private readonly DesktopSessionController _controller;
    private readonly SemaphoreSlim _configSaveGate = new(1, 1);
    private CalibrationConfig _calibration = new();
    private AppConfig _persistedConfig = AppConfig.Default;
    private ApplicationCandidate? _selectedCandidate;
    private LanguageOption _selectedLanguage =
        LocalizationService.SupportedLanguages[0];
    private ManagerPage _currentPage = ManagerPage.Appearance;
    private string _assistantName = "Codex";
    private string _userName = "You";
    private string _assistantAvatar = string.Empty;
    private string _userAvatar = string.Empty;
    private double _avatarSize = 40;
    private double _bubbleRadius = 14;
    private double _bubblePaddingX = 14;
    private double _bubblePaddingY = 10;
    private double _messageGap = 28;
    private double _messageMaxWidth = 66;
    private bool _nicknameVisible = true;
    private string _userBubble = "#242424";
    private string _assistantBubble = "#222222";
    private string _status = LocalizationService.Get("StatusStarting");
    private string? _statusKey = "StatusStarting";
    private object?[] _statusArguments = [];
    private string _diagnosticsText =
        LocalizationService.Get("DiagnosticsNotRefreshed");
    private bool _diagnosticsGenerated;
    private bool _initialized;
    private DesktopSessionState _sessionState =
        new(false, false, null, 0, "Not connected", null, null);

    public MainWindowViewModel()
    {
        _configStore = new ConfigStore(_paths);
        _avatarService = new AvatarService(_paths.AvatarsDirectory);
        _controller = new DesktopSessionController(RuntimeResourceLoader.Load());
        _controller.StateChanged += HandleStateChanged;
        _controller.RuntimeEventReceived += HandleRuntimeEvent;

        SelectAppearanceCommand = new RelayCommand(() => CurrentPage = ManagerPage.Appearance);
        SelectCalibrationCommand = new RelayCommand(() => CurrentPage = ManagerPage.Calibration);
        SelectDiagnosticsCommand = new RelayCommand(() => CurrentPage = ManagerPage.Diagnostics);
        SelectAboutCommand = new RelayCommand(() => CurrentPage = ManagerPage.About);
        DetectCommand = new AsyncRelayCommand(() => GuardAsync(DetectAsync, "ErrorDesktopDetection"));
        StartCommand = new AsyncRelayCommand(() => GuardAsync(StartAsync, "ErrorStartDesktop"));
        SaveCommand = new AsyncRelayCommand(() => GuardAsync(SaveAndApplyAsync, "ErrorSaveAppearance"));
        EnableCommand = new AsyncRelayCommand(() => GuardAsync(
            () => _controller.EnableSkinAsync(),
            "ErrorEnableSkin"));
        DisableCommand = new AsyncRelayCommand(() => GuardAsync(
            () => _controller.DisableSkinAsync(),
            "ErrorDisableSkin"));
        PickAssistantAvatarCommand = new AsyncRelayCommand(() => GuardAsync(
            () => PickAvatarAsync(true),
            "ErrorImportAssistantAvatar"));
        PickUserAvatarCommand = new AsyncRelayCommand(() => GuardAsync(
            () => PickAvatarAsync(false),
            "ErrorImportUserAvatar"));
        CalibrateAssistantCommand = new AsyncRelayCommand(() => GuardAsync(
            () => _controller.StartCalibrationAsync("assistant"),
            "ErrorCalibrateAssistant"));
        CalibrateUserCommand = new AsyncRelayCommand(() => GuardAsync(
            () => _controller.StartCalibrationAsync("user"),
            "ErrorCalibrateUser"));
        RefreshDiagnosticsCommand = new AsyncRelayCommand(() => GuardAsync(
            RefreshDiagnosticsAsync,
            "ErrorReadDiagnostics"));
        ResetAppearanceCommand = new RelayCommand(ResetAppearance);
        OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder);
    }

    public ObservableCollection<ApplicationCandidate> Candidates { get; } = [];
    public IReadOnlyList<LanguageOption> SupportedLanguages =>
        LocalizationService.SupportedLanguages;

    public ICommand SelectAppearanceCommand { get; }
    public ICommand SelectCalibrationCommand { get; }
    public ICommand SelectDiagnosticsCommand { get; }
    public ICommand SelectAboutCommand { get; }
    public ICommand DetectCommand { get; }
    public ICommand StartCommand { get; }
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

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || !Set(ref _selectedLanguage, value))
            {
                return;
            }
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
        set => Set(ref _selectedCandidate, value);
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
            Raise(nameof(IsAppearancePage));
            Raise(nameof(IsCalibrationPage));
            Raise(nameof(IsDiagnosticsPage));
            Raise(nameof(IsAboutPage));
        }
    }

    public bool IsAppearancePage => CurrentPage == ManagerPage.Appearance;
    public bool IsCalibrationPage => CurrentPage == ManagerPage.Calibration;
    public bool IsDiagnosticsPage => CurrentPage == ManagerPage.Diagnostics;
    public bool IsAboutPage => CurrentPage == ManagerPage.About;

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

    public double MessageMaxWidth
    {
        get => _messageMaxWidth;
        set
        {
            if (Set(ref _messageMaxWidth, value))
            {
                Raise(nameof(MessageMaxWidthLabel));
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
        set => Set(ref _assistantBubble, value);
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
            Raise(nameof(SessionStatus));
        }
    }

    public bool IsConnected => SessionState.IsConnected;
    public bool IsSkinEnabled => SessionState.IsSkinEnabled;
    public string ConnectionSummary =>
        SessionState.IsConnected
            ? LocalizationService.Format(
                "ConnectionConnectedFormat",
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
    public string AvatarSizeLabel =>
        LocalizationService.Format("AvatarSizeFormat", AvatarSize);
    public string BubbleRadiusLabel =>
        LocalizationService.Format("BubbleRadiusFormat", BubbleRadius);
    public string BubblePaddingXLabel =>
        LocalizationService.Format("HorizontalPaddingFormat", BubblePaddingX);
    public string BubblePaddingYLabel =>
        LocalizationService.Format("VerticalPaddingFormat", BubblePaddingY);
    public string MessageGapLabel =>
        LocalizationService.Format("MessageGapFormat", MessageGap);
    public string MessageMaxWidthLabel =>
        LocalizationService.Format("MaxWidthFormat", MessageMaxWidth);

    public bool WasFirstRun { get; private set; }

    public async Task InitializeAsync()
    {
        var load = await _configStore.LoadAsync().ConfigureAwait(true);
        WasFirstRun = load.WasCreated;
        _persistedConfig = load.Config;
        LoadConfig(load.Config);
        await DetectAsync().ConfigureAwait(true);
        SetStatus(
            load.CorruptBackupPath is null
                ? "StatusReady"
                : "StatusRecoveredConfig");
        _initialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        _controller.StateChanged -= HandleStateChanged;
        _controller.RuntimeEventReceived -= HandleRuntimeEvent;
        await _controller.DisposeAsync().ConfigureAwait(false);
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

    private async Task StartAsync()
    {
        var candidate = SelectedCandidate
                        ?? throw new InvalidOperationException("No Desktop candidate is selected.");
        candidate = await RefreshCandidateAsync(candidate).ConfigureAwait(true);
        if (candidate.IsRunning)
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
            SetStatus("StatusNormalShutdown");
            var closed = await _restartService.RequestGracefulCloseAsync(
                candidate,
                TimeSpan.FromSeconds(12)).ConfigureAwait(true);
            if (!closed)
            {
                var force = System.Windows.MessageBox.Show(
                    LocalizationService.Get("ForceRestartPrompt"),
                    LocalizationService.Get("ForceRestartTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (force != MessageBoxResult.Yes)
                {
                    SetStatus("StatusRestartCancelledUnchanged");
                    return;
                }
                await _restartService.ForceCloseAsync(candidate).ConfigureAwait(true);
            }
            await DetectAsync().ConfigureAwait(true);
            candidate = SelectedCandidate
                        ?? throw new InvalidOperationException(
                            "Desktop was not detected after restart.");
        }

        var adapter = _adapters.Select(candidate)
                      ?? throw new NotSupportedException(
                          "No compatible application adapter is available.");
        SetStatus("StatusStartingDesktop");
        try
        {
            await _controller.StartAsync(candidate, adapter, BuildConfig()).ConfigureAwait(true);
        }
        catch (FileNotFoundException)
        {
            SetStatus("StatusRefreshingDesktopEntry");
            candidate = await RefreshCandidateAsync(candidate).ConfigureAwait(true);
            adapter = _adapters.Select(candidate)
                      ?? throw new NotSupportedException(
                          "No compatible application adapter is available.");
            await _controller.StartAsync(candidate, adapter, BuildConfig())
                .ConfigureAwait(true);
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
        if (_controller.State.IsConnected)
        {
            await _controller.ApplyConfigAsync(config).ConfigureAwait(true);
        }
        SetStatus("StatusAppearanceSaved");
    }

    private async Task PickAvatarAsync(bool assistant)
    {
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
        var imported = await _avatarService.ImportAsync(picker.FileName).ConfigureAwait(true);
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

    private async Task RefreshDiagnosticsAsync()
    {
        var runtime = _controller.State.IsConnected
            ? await _controller.GetDiagnosticsAsync().ConfigureAwait(true)
            : [];
        DiagnosticsText = JsonSerializer.Serialize(new
        {
            managerVersion = "0.1.1-alpha",
            protocolVersion = 1,
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
        return new AppConfig
        {
            Language = SelectedLanguage.Code,
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
                AvatarSize = (int)Math.Round(AvatarSize),
                BubbleRadius = (int)Math.Round(BubbleRadius),
                BubblePaddingX = (int)Math.Round(BubblePaddingX),
                BubblePaddingY = (int)Math.Round(BubblePaddingY),
                NicknameVisible = NicknameVisible,
                MessageGap = (int)Math.Round(MessageGap),
                MessageMaxWidth = (int)Math.Round(MessageMaxWidth),
                UserBubble = UserBubble,
                AssistantBubble = AssistantBubble
            },
            Calibration = _calibration
        };
    }

    private void LoadConfig(AppConfig config)
    {
        var language = LocalizationService.SupportedLanguages.First(
            option => string.Equals(
                option.Code,
                LanguageCodes.Normalize(config.Language),
                StringComparison.OrdinalIgnoreCase));
        _selectedLanguage = language;
        LocalizationService.ApplyLanguage(language.Code);
        Raise(nameof(SelectedLanguage));
        AssistantName = config.Assistant.Name;
        UserName = config.User.Name;
        AssistantAvatar = config.Assistant.Avatar;
        UserAvatar = config.User.Avatar;
        AvatarSize = config.Appearance.AvatarSize;
        BubbleRadius = config.Appearance.BubbleRadius;
        BubblePaddingX = config.Appearance.BubblePaddingX;
        BubblePaddingY = config.Appearance.BubblePaddingY;
        MessageGap = config.Appearance.MessageGap;
        MessageMaxWidth = config.Appearance.MessageMaxWidth;
        NicknameVisible = config.Appearance.NicknameVisible;
        UserBubble = config.Appearance.UserBubble;
        AssistantBubble = config.Appearance.AssistantBubble;
        _calibration = config.Calibration;
        RefreshLocalizedProperties();
    }

    private async Task GuardAsync(Func<Task> operation, string contextKey)
    {
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            var context = LocalizationService.Get(contextKey);
            SetStatusText($"{context}: {exception.Message}");
            System.Windows.MessageBox.Show(
                exception.Message,
                context,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void HandleStateChanged(object? sender, DesktopSessionState state)
    {
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
                if (signature is null || signature.SchemaVersion != 1)
                {
                    throw new InvalidOperationException(
                        "Calibration signature is not supported.");
                }
                _calibration = role == "assistant"
                    ? _calibration with { AssistantTurn = signature }
                    : _calibration with { UserTurn = signature };
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
                SetStatus(
                    "StatusCalibrationRejectedFormat",
                    exception.Message);
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

    private async Task PersistLanguageAsync(string language)
    {
        try
        {
            await _configSaveGate.WaitAsync().ConfigureAwait(true);
            try
            {
                var config = _persistedConfig with
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
            SetStatus("StatusLanguageSaved");
        }
        catch (Exception exception)
        {
            SetStatusText(
                $"{LocalizationService.Get("ErrorSaveLanguage")}: {exception.Message}");
        }
    }

    private void RefreshLocalizedProperties()
    {
        Raise(nameof(ConnectionSummary));
        Raise(nameof(CalibrationSummary));
        Raise(nameof(SessionStatus));
        Raise(nameof(AvatarSizeLabel));
        Raise(nameof(BubbleRadiusLabel));
        Raise(nameof(BubblePaddingXLabel));
        Raise(nameof(BubblePaddingYLabel));
        Raise(nameof(MessageGapLabel));
        Raise(nameof(MessageMaxWidthLabel));
        if (_statusKey is not null)
        {
            Status = LocalizationService.Format(_statusKey, _statusArguments);
        }
        if (!_diagnosticsGenerated)
        {
            DiagnosticsText = LocalizationService.Get("DiagnosticsNotRefreshed");
        }
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
            "Skin active" => "StatusSkinActive",
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
            "Skin active" => "StatusSkinActive",
            "Safe mode: no compatible renderer" => "StatusSafeMode",
            _ => null
        };
        return key is null ? status : LocalizationService.Get(key);
    }
}
