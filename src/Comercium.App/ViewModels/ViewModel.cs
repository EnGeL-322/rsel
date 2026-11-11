using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public abstract class ViewModel(DesktopDialogs dialogs) : INotifyPropertyChanged
{
    private string message = "";
    private bool hasError;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Message { get => message; private set => Set(ref message, value); }
    public bool HasError { get => hasError; private set => Set(ref hasError, value); }
    protected DesktopDialogs Dialogs { get; } = dialogs;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        Notify(name);
    }

    protected void Notify([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        CommandManager.InvalidateRequerySuggested();
    }

    protected void Show(string text, bool error = false)
    {
        HasError = error;
        Message = text;
    }

    protected void Run(Action action)
    {
        Show("");
        try { action(); }
        catch (BusinessException error) { Show(error.Message, true); }
        catch (Exception error)
        {
            Dialogs.RecordError(error);
            Show("Ocurrió un error al procesar la operación. Actualiza la lista antes de intentarlo de nuevo.", true);
        }
    }

    protected ICommand Action(Action run, Func<bool>? enabled = null) => new UiCommand(() => Run(run), enabled);

    private sealed class UiCommand(Action run, Func<bool>? enabled) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public bool CanExecute(object? parameter) => enabled?.Invoke() ?? true;
        public void Execute(object? parameter) => run();
    }
}
