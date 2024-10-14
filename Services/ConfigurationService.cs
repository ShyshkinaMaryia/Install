using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class ConfigurationService
{
    public async Task InstallAsync(string almazCodPath, string selectedFilePath, Action<int> onProgressChanged, Action<string> onStatusChanged, CancellationToken cancellationToken)
    {
        // Проверяем, указан ли путь к папке установки
        if (string.IsNullOrEmpty(almazCodPath))
        {
            onStatusChanged("Путь к папке не указан.");
            return;
        }

        try
        {
            onStatusChanged("Распаковка COD.ZIP, cifStandart4RIR.zip, cctbx_Fixed.zip...");

            // Создаем экземпляр службы распаковки и распаковываем ALMAZ_COD.ZIP
            UnzipService unzip = new UnzipService();

            // Массив с именами архивов для распаковки
            string[] zipFiles = { "COD.ZIP", "cifStandart4RIR.zip", "cctbx_Fixed.zip" };
            foreach (var zipFileName in zipFiles)
            {
                string zipFilePath = Path.Combine(almazCodPath, zipFileName);

                // Проверяем наличие каждого архива
                if (!File.Exists(zipFilePath))
                {
                    onStatusChanged($"Архив {zipFileName} не найден.");
                    continue;
                }
                // Распаковываем архив
                await unzip.UnzipFileAsync(zipFilePath, almazCodPath, almazCodPath, onProgressChanged, onStatusChanged, cancellationToken);
            }

            // Настройки для редактирования файлов конфигурации
            var settingsFiles = new[]
            {
            new { FileName = "1stProcessSettingsAllCifs.txt", FirstArray = new[] { 0, 4, 8 }, SecondArray = new[] { 3, 7, 11 }, Value = 1 },
            new { FileName = "2ndProcessSettingsAllCifs.txt", FirstArray = new[] { 0, 4 }, SecondArray = new[] { 3, 7 }, Value = 4 },
            new { FileName = "3rdProcessSettingsAllCifs.txt", FirstArray = new[] { 0, 4 }, SecondArray = new[] { 3, 7 }, Value = 6 },
            new { FileName = "4thProcessSettingsAllCifs.txt", FirstArray = new[] { 0, 4 }, SecondArray = new[] { 3, 7 }, Value = 8 }
            };

            // Путь к папке cifStandart4RIR
            string cS4RIRPath = Path.Combine(selectedFilePath, "cifStandart4RIR");
            string cifFileName = "al2o3_73724.cif";

            onStatusChanged("Редактирование файлов...");

            // Редактируем файлы настроек
            foreach (var settings in settingsFiles)
            {
                await UpdateSettingsFile(almazCodPath, selectedFilePath, cifFileName, cS4RIRPath, settings.FileName,
                    settings.FirstArray, settings.SecondArray, settings.Value, onStatusChanged, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Редактируем batch-файл для запуска AllCifs
            string batchFilePath = Path.Combine(selectedFilePath, "COD", "runAllCifs.bat");
            await EditBatchFileAsync(batchFilePath, selectedFilePath, onStatusChanged, cancellationToken);

            // Проверяем запрос на отмену операции
            cancellationToken.ThrowIfCancellationRequested();

            // Строка для добавления в Python скрипты
            string codingPy = "# -*- coding: utf-8 -*-";
            // Массив с именами Python скриптов для редактирования
            string[] pythonScripts = { "TreeWalker.py", "TreeWalkerDelEmptyFolders.py", "TreeWalkerSelector.py", "Wavelength.py" };

            // Редактируем каждый из Python скриптов
            foreach (var script in pythonScripts)
            {
                string scriptPath = Path.Combine(selectedFilePath, "COD", script);
                await EditFileAsync(scriptPath, codingPy, onStatusChanged, cancellationToken);
            }

            // Запускаем batch-файл runAllCifs.bat
            onStatusChanged("Запуск runAllCifs.bat...");
            RunBatchFile(batchFilePath, onStatusChanged);

            // Путь к директории cctbx
            string cctbxPath = Path.Combine(selectedFilePath, "cctbx");

            string pathScript = Path.Combine(cctbxPath, "cctbx_install_script.bat");

            // Запускаем batch-файл cctbx_install_script.bat
            onStatusChanged("Запуск cctbx_install_script.bat");
            RunBatchFile(pathScript, onStatusChanged);
        }
        catch (OperationCanceledException)
        {
            onStatusChanged("Установка была отменена.");
        }
        catch (Exception ex)
        {
            onStatusChanged($"Ошибка: {ex.Message}");
        }
    }

    private async Task EditBatchFileAsync(string batchFilePath, string almazCodPath, Action<string> onStatusChanged, CancellationToken cancellationToken)
    {
        // Определяем строку для установки пути к cctbxPython
        string cctbxPythonLocation = "set cctbxPython=";
        // Формируем полный путь к папке, где находится cctbxPython
        string cctbxPythonPath = Path.Combine(almazCodPath, "cctbx", "cctbx_build", "bin");
        // Имя файла cctbx.python.bat
        string cctbxPythonName = "cctbx.python.bat";

        // Уведомляем о начале редактирования файла
        onStatusChanged?.Invoke($"Редактирование файла: {batchFilePath}");

        // Читаем все строки из указанного batch-файла асинхронно
        string[] lines = await Task.Run(() => File.ReadAllLines(batchFilePath, Encoding.UTF8), cancellationToken);
        // Изменяем первую строку, добавляя путь к cctbxPython
        lines[0] = cctbxPythonLocation + Path.Combine(cctbxPythonPath, cctbxPythonName);

        // Определяем строку для установки пути к CODprocessor
        string codLocation = "set CODprocessorPath=";
        // Формируем полный путь к папке COD
        string combinedPath = Path.Combine(almazCodPath, "COD");
        // Изменяем вторую строку, добавляя путь к COD
        lines[1] = codLocation + combinedPath + @"\";

        // Записываем измененные строки обратно в файл асинхронно
        await Task.Run(() => File.WriteAllLines(batchFilePath, lines, new UTF8Encoding(false)), cancellationToken);
    }

    // Асинхронный метод для обновления файла настроек
    async Task UpdateSettingsFile(string folderPath, string almazCodPath, string cifFileName, string cifStandart4RIRPath, string fileName,
         int[] baseIndices, int[] indicesToUpdate, int startIndex, Action<string> updateStatus, CancellationToken cancellationToken)
    {
        // Формируем полный путь к файлу настроек
        string settingsFilePath = Path.Combine(almazCodPath, "COD", fileName);

        // Читаем все строки из файла настроек в асинхронном режиме
        string[] lines = await Task.Run(() => File.ReadAllLines(settingsFilePath, Encoding.UTF8));

        // Обновляем строки в файле настроек на основе массива baseIndices
        for (int i = 0; i < baseIndices.Length; i++)
        {
            int indexOffset = startIndex + i;
            // Обновляем пути в строках 
            lines[baseIndices[i]] = Path.Combine(folderPath, "cif", indexOffset.ToString());
            lines[baseIndices[i] + 1] = Path.Combine(folderPath, "cif_hkl", indexOffset.ToString());
            lines[baseIndices[i] + 2] = Path.Combine(folderPath, "cif_corrupted", indexOffset.ToString());
        }

        // Обновляем строки в файле настроек на основе массива indicesToUpdate
        foreach (var index in indicesToUpdate)
        {
            // Устанавливаем путь для каждой строки из indicesToUpdate
            lines[index] = Path.Combine(cifStandart4RIRPath, cifFileName);
        }

        // Записываем обновленные строки обратно в файл настроек в асинхронном режиме
        await Task.Run(() => File.WriteAllLines(settingsFilePath, lines, new UTF8Encoding(false)));

        // Вызываем делегат для обновления статуса с сообщением об успешном обновлении файла настроек
        updateStatus?.Invoke($"Обновление файла настроек: {settingsFilePath}");

        // Проверяем, был ли запрошен токен отмены и выбрасываем исключение, если это так
        cancellationToken.ThrowIfCancellationRequested();
    }

    // Метод для запуска batch-файла
    private void RunBatchFile(string batchFilePath, Action<string> onStatusChanged)
    {
        try
        {
            string workingDirectory = System.IO.Path.GetDirectoryName(batchFilePath);
    
            string[] lines = File.ReadAllLines(batchFilePath, Encoding.UTF8);
    
            // Вставка строки кодировки "chcp 65001 >nul" в первую позицию
            var linesList = new List<string>(lines);
            linesList.Insert(0, "chcp 65001 >nul");
            lines = linesList.ToArray();
    
            File.WriteAllLines(batchFilePath, lines, new UTF8Encoding(false));
    
            var processStartInfo = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/C \"{batchFilePath}\"",
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = workingDirectory
            };
    
            using (Process process = Process.Start(processStartInfo))
            {
                process.WaitForExit();
    
                if (process == null)
                {
                    onStatusChanged("Не удалось запустить процесс.");
                }
            }
        }
        catch (FileNotFoundException ex)
        {
            onStatusChanged($"Файл не найден: {ex.Message}");
        }
        catch (Win32Exception ex)
        {
            onStatusChanged($"Ошибка при запуске процесса: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            onStatusChanged($"Ошибка выполнения: {ex.Message}");
        }
        catch (Exception ex)
        {
            onStatusChanged($"Неизвестная ошибка: {ex.Message}");
        }
    }

    // Асинхронный метод для редактирования файла, добавляющий заголовок в начало файла
    static async Task EditFileAsync(string filePath, string header, Action<string> onStatusChanged, CancellationToken cancellationToken)
    {
        // Читаем все строки из файла в асинхронном режиме
        string[] lines = await Task.Run(() => File.ReadAllLines(filePath, Encoding.UTF8));

        // Создаем список строк на основе массива строк, прочитанных из файла
        var linesList = new List<string>(lines);

        // Вставляем заголовок в начало списка строк
        linesList.Insert(0, header);

        // Записываем обновленный список строк обратно в файл в асинхронном режиме
        await Task.Run(() => File.WriteAllLines(filePath, linesList.ToArray(), new UTF8Encoding(false)));

        onStatusChanged?.Invoke($"Редактирование файла: {filePath}");

        // Проверяем, был ли запрошен токен отмены и выбрасываем исключение, если это так
        cancellationToken.ThrowIfCancellationRequested();
    }

    // Метод для проверки корректности входных данных
    public void ValidateInput(string zipFilePath, string destinationFolderPath)
    {
        // Проверяем, что путь к zip файлу не пустой или не содержит только пробелы
        if (string.IsNullOrWhiteSpace(zipFilePath))
            throw new ArgumentException("Путь к zip файлу не может быть пустым.", nameof(zipFilePath));

        // Проверяем, что путь к папке назначения не пустой или не содержит только пробелы
        if (string.IsNullOrWhiteSpace(destinationFolderPath))
            throw new ArgumentException("Путь к папке назначения не может быть пустым.", nameof(destinationFolderPath));

        // Проверяем, что файл существует по указанному пути
        if (!File.Exists(zipFilePath))
            throw new FileNotFoundException("Zip файл не найден.", zipFilePath);
    }
}
