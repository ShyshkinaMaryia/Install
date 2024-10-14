using System;
using System.Threading;

// Основной класс контроллера операций
public class OperationController
{
    public readonly CancellationTokenSource cancellationTokenSource;
    public readonly StatusUpdater statusUpdater;
    public readonly Action updateCommands;


    public OperationController(CancellationTokenSource cancellationTokenSource, StatusUpdater statusUpdater, Action updateCommands)
    {
        this.cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));
        this.statusUpdater = statusUpdater ?? throw new ArgumentNullException(nameof(statusUpdater));
        this.updateCommands = updateCommands ?? throw new ArgumentNullException(nameof(updateCommands));
    }

    public OperationController()
    {
    }

    public void ExecuteCancel()
    {
        if (CanCancel())
        {
            cancellationTokenSource.Cancel();
            statusUpdater.UpdateStatus("Операция отменена.");
            updateCommands();
        }
    }
    public bool CanCancel()
    {
        return cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested;
    }
}

// Класс для обновления статуса в статус-баре
public class StatusUpdater
{
    private readonly Action<string> onStatusChanged;

    public StatusUpdater(Action<string> onStatusChanged)
    {
        this.onStatusChanged = onStatusChanged ?? throw new ArgumentNullException(nameof(onStatusChanged));
    }

    public void UpdateStatus(string statusMessage)
    {
        onStatusChanged?.Invoke(statusMessage);
    }
    public void UpdateProgress(int processedFiles, int totalFiles, string entryFileName, Action<int> onProgressChanged, Action<string> onStatusChanged)
    {
        int progressPercentage = (int)((double)processedFiles / totalFiles * 100);
        onProgressChanged(progressPercentage);
        onStatusChanged($"Распаковка: {entryFileName}");
    }
}

