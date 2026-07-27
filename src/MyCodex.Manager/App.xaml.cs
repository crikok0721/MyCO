using System.IO;
using System.Threading;
using System.Text.Json;
using System.Windows;
using MyCodex.Configuration;
using MyCodex.Diagnostics;
using MyCodex.Manager.Localization;
using MyCodex.Manager.ViewModels;
using MyCodex.Manager.Views;

// WPF application entry point: language, single-instance guard, setup, and main window.
namespace MyCodex.Manager;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private bool _ownsMutex;
    private IPrivacySafeLogger? _logger;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        _logger = TryCreateLogger();
        DispatcherUnhandledException += HandleDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += HandleDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
        // Apply language before any window is created so startup dialogs are localized too.
        TryApplyStoredLanguage();
        _singleInstance = new Mutex(
            initiallyOwned: true,
            "Local\\MyCodex.Manager.0.1",
            out _ownsMutex);
        if (!_ownsMutex)
        {
            System.Windows.MessageBox.Show(
                LocalizationService.Get("AlreadyRunningMessage"),
                "MyCodex",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            // Keep the app alive while the first-run dialog temporarily owns the UI.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var viewModel = new MainWindowViewModel();
            await viewModel.InitializeAsync();
            if (viewModel.WasFirstRun)
            {
                new OnboardingWindow(viewModel).ShowDialog();
            }
            var window = new MainWindow(viewModel);
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
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
