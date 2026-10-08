using System.Windows.Input;

namespace FbxToMua.App.ViewModels;

public sealed class AsyncCommand(Func<Task> run, Func<bool>? canRun = null) : ICommand
{
    private bool _running;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_running && (canRun?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _running = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await run();
        }
        finally
        {
            _running = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
