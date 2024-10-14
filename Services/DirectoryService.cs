using Microsoft.Win32;
using System;
using System.IO;
using System.IO.Compression;
using WinForm = System.Windows.Forms;

public class DirectoryService
{
    // Метод для выбора папки для сохранения загруженного файла
    public string SelectDownloadFolder()
    {
        using (var folderDialog = new WinForm.FolderBrowserDialog())
        {
            WinForm.DialogResult result = folderDialog.ShowDialog();
            if (result == WinForm.DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
            {
                return folderDialog.SelectedPath;
            }
        }

        // Возвращаем пустую строку, если выбор папки был отменен
        return string.Empty;
    }

    // Метод для выбора ZIP-файла с использованием диалогового окна 
    public string SelectZipFile(string initialFolderPath, Action<string> onStatusChanged, out string folderPath)
    {
        // Уведомляем пользователя о начале процесса выбора файла 
        onStatusChanged("Выбор архива ALMAZ_COD.ZIP");

        // Создаем объект диалогового окна для выбора файла 
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            InitialDirectory = initialFolderPath,
            Filter = "ZIP files (*.zip)|*.zip",
            Title = "Выберите архив ALMAZ_COD"
        };

        // Открываем диалоговое окно и получаем результат выбора 
        bool? result = openFileDialog.ShowDialog();
        if (result != true)
        {
            // Если файл не был выбран, уведомляем об этом пользователя и возвращаем null 
            onStatusChanged("Файл не был выбран.");
            folderPath = null;
            return null;
        }
        folderPath = openFileDialog.FileName;

        // Возвращаем полный путь к выбранному файлу 
        return openFileDialog.FileName;
    }

    // Метод для выбора директории назначения с использованием диалогового окна
    public static string SelectDestinationFolder(string initialFolderPath)
    {
        // Создаем объект диалогового окна для выбора папки
        using (var folderDialog = new WinForm.FolderBrowserDialog())
        {
            folderDialog.SelectedPath = initialFolderPath; // Устанавливаем начальный путь в диалоге
            WinForm.DialogResult result = folderDialog.ShowDialog(); // Открываем диалоговое окно и получаем результат выбора

            // Проверяем, была ли выбрана папка и является ли путь непустым
            if (result == WinForm.DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
            {
                return folderDialog.SelectedPath; // Возвращаем выбранный путь, если он валиден
            }
        }
        return string.Empty; // Возвращаем пустую строку, если выбор был отменен
    }

    // Метод для проверки существования директории и её создания при необходимости
    public void EnsureDirectoryExists(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Путь к директории не может быть пустым.", nameof(directoryPath));

        // Проверяем, существует ли директория
        if (!Directory.Exists(directoryPath))
        {
            // Если директория не существует, создаем её
            Directory.CreateDirectory(directoryPath);
        }
    }

    // Метод для проверки, достаточно ли свободного места на диске для распаковки архива
    public bool IsEnoughDiskSpace(string zipFilePath, string destinationFolderPath)
    {
        // Вычисляем общий размер всех файлов в архиве
        long totalUncompressedSize = CalculateTotalSize(zipFilePath);
        // Получаем доступное свободное место на диске по указанному пути
        long availableSpace = GetAvailableDiskSpace(destinationFolderPath);
        // Возвращаем true, если доступного места достаточно для распаковки архива
        return availableSpace >= totalUncompressedSize;
    }

    // Метод для вычисления общего размера всех файлов в архиве
    private long CalculateTotalSize(string zipFilePath)
    {
        using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
        {
            long totalSize = 0;
            // Проходим по всем записям в архиве и суммируем их размеры
            foreach (var entry in archive.Entries)
            {
                totalSize += entry.Length;
            }
            // Возвращаем общий размер файлов в архиве
            return totalSize;
        }
    }

    // Метод для получения доступного свободного места на диске по указанному пути
    private long GetAvailableDiskSpace(string folderPath)
    {
        // Получаем информацию о диске, на котором находится указанная директория
        DriveInfo drive = new DriveInfo(Path.GetPathRoot(folderPath));
        // Возвращаем количество доступного свободного места на диске
        return drive.AvailableFreeSpace;
    }
}

