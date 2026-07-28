using System.IO;
using System.Threading;
using System.Text.Json;
using System.Windows;
using MyCodex.Configuration;
using MyCodex.Diagnostics;
using MyCodex.Manager.Localization;
using MyCodex.Manager.Services;
using MyCodex.Manager.ViewModels;
using MyCodex.Manager.Views;

// WPF application entry point: language, single-instance guard, setup, and main window.
namespace MyCodex.Manager;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\MyCodex.Manager.0.2";
    private const string ActivationEventName = "Local\\MyCodex.Manager.Activate.0.2";
    private Mutex? _singleInstance;
    private EventWaitHandle? _activationEvent;
    private EventWaitHandle? _activationStop;
    private Task? _activationTask;
    private bool _ownsMutex;
    private IPrivacySafeLogger? _logger;
    private TrayService? _trayService;

    internal ThemeService ThemeService { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        _logger = TryCreateLogger();
        DispatcherUnhandledException += HandleDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += HandleDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ThemeService = new ThemeService();
        ThemeService.ApplyMode(ManagerThemeMode.System);
        // Apply language before any window is created so startup dialogs are localized too.
        TryApplyStoredLanguage();
        _singleInstance = new Mutex(
            initiallyOwned: true,
            MutexName,
            out _ownsMutex);
        _activationEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            ActivationEventName);
        if (!_ownsMutex)
        {
            _activationEvent.Set();
            Shutdown();
            return;
        }

        try
        {
            var viewModel = new MainWindowViewModel();
            await viewModel.InitializeAsync();
            var background = StartupPresentation.StartsInBackground(eventArgs.Args);
            if (viewModel.WasFirstRun && !background)
            {
                new OnboardingWindow(viewModel).ShowDialog();
            }
            var window = new MainWindow(viewModel);
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            _trayService = new TrayService(window, viewModel, ThemeService);
            StartActivationListener();
            if (background)
            {
                window.PrepareForBackground();
            }
            else
            {
                window.Show();
            }
            await viewModel.StartAutomaticallyIfConfiguredAsync().ConfigureAwait(true);
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

    private void StartActivationListener()
    {
        _activationStop = new EventWaitHandle(false, EventResetMode.ManualReset);
        _activationTask = Task.Run(() =>
        {
            var handles = new WaitHandle[] { _activationEvent!, _activationStop };
            while (WaitHandle.WaitAny(handles) == 0)
            {
                _ = Dispatcher.InvokeAsync(() => _trayService?.ShowWindow());
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
            "MyCodex",
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
