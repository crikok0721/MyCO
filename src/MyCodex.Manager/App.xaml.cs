using System.IO;
using System.Threading;
using System.Text.Json;
using System.Windows;
using MyCodex.Configuration;
using MyCodex.Manager.Localization;
using MyCodex.Manager.ViewModels;
using MyCodex.Manager.Views;

namespace MyCodex.Manager;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private bool _ownsMutex;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
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
            System.Windows.MessageBox.Show(
                exception.ToString(),
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
        base.OnExit(eventArgs);
    }

    private static void TryApplyStoredLanguage()
    {
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
