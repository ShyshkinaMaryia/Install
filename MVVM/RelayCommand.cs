using System;
using System.Threading.Tasks;
using System.Windows.Input;

public class RelayCommand : ICommand
{
    private readonly Func<bool> canExecute;
    private readonly Action execute;
    private readonly Func<Task> executeAsync;

    public event EventHandler CanExecuteChanged;

    public RelayCommand(Action execute, Func<bool> canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
    {
        this.executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        this.canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => canExecute == null || canExecute();

    public void Execute(object parameter)
    {
        if (execute != null)
        {
            execute();
        }
        else if (executeAsync != null)
        {
            ExecuteAsync().ConfigureAwait(false);
        }
    }

    private async Task ExecuteAsync()
    {
        try
        {
            if (executeAsync != null)
            {
                await executeAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при выполнении команды: {ex.Message}");
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
