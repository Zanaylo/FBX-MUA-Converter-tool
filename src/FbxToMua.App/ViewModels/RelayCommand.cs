using System.Windows.Input;

namespace FbxToMua.App.ViewModels;

public sealed class RelayCommand(Action run) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => run();
}
