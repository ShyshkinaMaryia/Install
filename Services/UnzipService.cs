using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class UnzipService
{
    private readonly DirectoryService directoryService = new DirectoryService();
    private readonly ConfigurationService configurationService = new ConfigurationService();

    // Асинхронный метод для распаковки файла
    public async Task UnzipFileAsync(string zipFilePath, string initialFolderPath, string destinationFolderPath, Action<int> onProgressChanged, Action<string> onStatusChanged, CancellationToken cancellationToken)
    {
        // Уведомление о начале процесса распаковки
        onStatusChanged("Начало процесса распаковки...");

        // Проверка входных параметров
        configurationService.ValidateInput(zipFilePath, initialFolderPath);

        // Проверка наличия достаточного места на диске
        if (!directoryService.IsEnoughDiskSpace(zipFilePath, destinationFolderPath))
        {
            onStatusChanged("Недостаточно места на диске для распаковки архива.");
            return;
        }

        // Запуск распаковки в отдельном потоке
        await Task.Run(() => UnzipFiles(zipFilePath, destinationFolderPath, onProgressChanged, onStatusChanged, cancellationToken), cancellationToken);
    }

    // Метод для распаковки файлов из архива
    private void UnzipFiles(string zipFilePath, string destinationFolderPath, Action<int> onProgressChanged, Action<string> onStatusChanged, CancellationToken cancellationToken)
    {
        try
        {
            // Создание директории назначения, если она не существует
            directoryService.EnsureDirectoryExists(destinationFolderPath);

            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                int totalFiles = archive.Entries.Count; // Общее количество файлов в архиве
                int processedFiles = 0; // Количество обработанных файлов
                var statusUpdater = new StatusUpdater(onStatusChanged); // Объект для обновления статуса

                foreach (var entry in archive.Entries)
                {
                    // Проверка на отмену операции
                    cancellationToken.ThrowIfCancellationRequested();

                    // Получение имени файла с декодированием
                    string entryFileName = GetEntryFileName(entry, onStatusChanged);
                    string destinationPath = Path.Combine(destinationFolderPath, entryFileName);

                    // Если это директория, создаем её и продолжаем
                    if (entry.FullName.EndsWith("/"))
                    {
                        directoryService.EnsureDirectoryExists(destinationPath);
                        continue;
                    }

                    // Убедиться, что директория назначения существует
                    directoryService.EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));
                    // Извлечение файла
                    entry.ExtractToFile(destinationPath, overwrite: true);

                    processedFiles++; // Увеличение счетчика обработанных файлов
                    // Обновление прогресса и статуса
                    statusUpdater.UpdateProgress(processedFiles, totalFiles, entryFileName, onProgressChanged, onStatusChanged);
                }

                // Уведомление об успешном завершении процесса распаковки
                onStatusChanged("Процесс распаковки успешно завершен.");
            }
        }
        catch (OperationCanceledException)
        {
            // Уведомление об отмене операции пользователем
            onStatusChanged("Распаковка отменена пользователем.");
        }
        catch (Exception ex)
        {
            // Уведомление об ошибке во время распаковки
            onStatusChanged($"Ошибка при распаковке: {ex.Message}");
            throw;
        }
    }
    // Метод для получения имени файла с декодированием
    private string GetEntryFileName(ZipArchiveEntry entry, Action<string> onStatusChanged)
    {
        try
        {
            byte[] bytes = Encoding.Default.GetBytes(entry.FullName); // Получение байтов из имени файла
            string decodedFileName = Encoding.GetEncoding("CP866").GetString(bytes); // Декодирование имени файла

            if (!string.IsNullOrWhiteSpace(decodedFileName) && !decodedFileName.Contains("?"))
            {
                return decodedFileName; // Возвращаем декодированное имя файла
            }
        }
        catch (Exception ex)
        {
            // Уведомление об ошибке декодирования
            onStatusChanged($"Ошибка: {ex.Message}");
        }

        return entry.FullName; // Возвращаем оригинальное имя файла в случае ошибки декодирования
    }
}