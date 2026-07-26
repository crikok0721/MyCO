using System.Windows.Input;

// ICommand wrapper for async UI actions; disables itself while the task is running.
namespace MyCodex.Manager.ViewModels;

internal sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _running;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_running && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        // ICommand requires void; exceptions are handled by the view model's GuardAsync wrapper.
        if (!CanExecute(parameter))
        {
            return;
        }
        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await _execute();
        }
        finally
        {
            _running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
