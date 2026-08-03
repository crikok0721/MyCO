using System.IO;
using System.Threading;
using System.Text.Json;
using System.Windows;
using MyCO.Configuration;
using MyCO.Diagnostics;
using MyCO.Manager.Localization;
using MyCO.Manager.Services;
using MyCO.Manager.ViewModels;
using MyCO.Manager.Views;
using MyCO.Startup;

// WPF application entry point: language, single-instance guard, setup, and main window.
namespace MyCO.Manager;

public partial class App : System.Windows.Application
{
    // Preserve the legacy kernel names so old and renamed builds cannot run together.
    private const string MutexName = "Local\\MyCodex.Manager.0.2";
    private const string ActivationEventName = "Local\\MyCodex.Manager.Activate.0.2";
    private const string CodexLaunchEventName = "Local\\MyCodex.Manager.CodexLaunch.0.2";
    private Mutex? _singleInstance;
    private EventWaitHandle? _activationEvent;
    private EventWaitHandle? _codexLaunchEvent;
    private EventWaitHandle? _activationStop;
    private Task? _activationTask;
    private bool _ownsMutex;
    private IPrivacySafeLogger? _logger;
    private TrayService? _trayService;

    internal ThemeService ThemeService { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        MyCOAppIdentity.Apply();
        ApplyMotionPreference();
        _logger = TryCreateLogger();
        DispatcherUnhandledException += HandleDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += HandleDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ThemeService = new ThemeService();
        ThemeService.ApplyMode(ManagerThemeMode.System);
        // Apply language before any window is created so startup dialogs are localized too.
        LocalizationService.ApplyLanguage(LanguageCodes.English);
        TryApplyStoredLanguage();
        _singleInstance = new Mutex(
            initiallyOwned: true,
            MutexName,
            out _ownsMutex);
        _activationEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            ActivationEventName);
        _codexLaunchEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            CodexLaunchEventName);
        if (!_ownsMutex)
        {
            if (StartupPresentation.IsCodexLaunch(eventArgs.Args))
            {
                _codexLaunchEvent.Set();
            }
            _activationEvent.Set();
            Shutdown();
            return;
        }

        try
        {
            var viewModel = new MainWindowViewModel();
            await viewModel.InitializeAsync();
            var background = StartupPresentation.StartsInBackground(eventArgs.Args);
            var associatedCodexLaunch = StartupPresentation.IsCodexLaunch(eventArgs.Args);
            if (viewModel.WasFirstRun && !background)
            {
                new OnboardingWindow(viewModel).ShowDialog();
            }
            var window = new MainWindow(viewModel);
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            _trayService = new TrayService(window, viewModel, ThemeService);
            StartActivationListener(viewModel);
            if (background)
            {
                window.PrepareForBackground();
            }
            else
            {
                window.Show();
            }
            if (associatedCodexLaunch)
            {
                await viewModel.StartFromAssociatedLaunchAsync().ConfigureAwait(true);
            }
            else
            {
                await viewModel.StartAutomaticallyIfConfiguredAsync().ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            var errorCode = ErrorCodeFactory.Create("APP", "STARTUP");
            _logger?.Error(errorCode, exception);
            System.Windows.MessageBox.Show(
                LocalizationService.Format("UnhandledErrorFormat", errorCode),
                LocalizationService.Get("StartupFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        _activationStop?.Set();
        try
        {
            _activationTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The process is already exiting; activation listener errors are non-fatal.
        }
        _activationStop?.Dispose();
        _codexLaunchEvent?.Dispose();
        _activationEvent?.Dispose();
        _trayService?.Dispose();
        ThemeService?.Dispose();
        if (_ownsMutex)
        {
            _singleInstance?.ReleaseMutex();
        }
        _singleInstance?.Dispose();
        DispatcherUnhandledException -= HandleDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= HandleDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= HandleUnobservedTaskException;
        base.OnExit(eventArgs);
    }

    private void StartActivationListener(MainWindowViewModel viewModel)
    {
        _activationStop = new EventWaitHandle(false, EventResetMode.ManualReset);
        _activationTask = Task.Run(() =>
        {
            var handles = new WaitHandle[]
            {
                _activationEvent!,
                _codexLaunchEvent!,
                _activationStop
            };
            while (true)
            {
                var signaled = WaitHandle.WaitAny(handles);
                if (signaled == 2)
                {
                    return;
                }
                if (signaled == 0)
                {
                    _ = Dispatcher.InvokeAsync(() => _trayService?.ShowWindow());
                    continue;
                }
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    _trayService?.ShowWindow();
                    await viewModel.StartFromAssociatedLaunchAsync();
                });
            }
        });
    }

    private void HandleDispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        var errorCode = ErrorCodeFactory.Create("UI", "UNHANDLED");
        _logger?.Error(errorCode, eventArgs.Exception);
        eventArgs.Handled = true;
        System.Windows.MessageBox.Show(
            LocalizationService.Format("UnhandledErrorFormat", errorCode),
            "MyCO",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(1);
    }

    private void HandleDomainUnhandledException(
        object? sender,
        UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
        {
            _logger?.Error(ErrorCodeFactory.Create("APP", "FATAL"), exception);
        }
    }

    private void HandleUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs eventArgs)
    {
        _logger?.Error(
            ErrorCodeFactory.Create("TASK", "UNOBSERVED"),
            eventArgs.Exception);
        eventArgs.SetObserved();
    }

    private static IPrivacySafeLogger? TryCreateLogger()
    {
        try
        {
            return new PrivacySafeLogger(new ConfigPaths().LogsDirectory);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void ApplyMotionPreference()
    {
        if (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
        {
            return;
        }
        var disabled = new Duration(TimeSpan.Zero);
        Resources["MotionInstantDuration"] = disabled;
        Resources["MotionFastDuration"] = disabled;
        Resources["MotionStandardDuration"] = disabled;
        Resources["MotionRelaxedDuration"] = disabled;
    }

    private static void TryApplyStoredLanguage()
    {
        // This lightweight read avoids constructing ConfigStore before the single-instance check.
        try
        {
            var path = new ConfigPaths().ConfigFile;
            if (!File.Exists(path))
            {
                return;
            }
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var language = document.RootElement.TryGetProperty(
                "language",
                out var property)
                ? property.GetString()
                : null;
            if (LanguageCodes.IsSupported(language))
            {
                LocalizationService.ApplyLanguage(language!);
            }
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // ConfigStore performs recovery after single-instance startup.
        }
    }
}
