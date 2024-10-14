using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WinForm = System.Windows.Forms;

public class MainWindowViewModel : INotifyPropertyChanged
{
    public string downloadUrl = "http://localhost/cod-cifs-mysql.zip"; // cod-cifs-mysql.zip" WpfAppTest.zip; "http://www.crystallography.net/archives/cod-cifs-mysql.zip";  URL для загрузки по умолчанию
    public int progress; // Переменная для хранения прогресса загрузки/разархивирования
    public string status; // Переменная для хранения статуса операции
    public string zipPath; // Переменная для хранения пути к загруженному zip-файлу библиотеки
    public string initialFolderPath; // Переменная для хранения пути, указанного пользователем в месте хранения программы ALMAZ_COD
    public string almazCodPath; // Переменная для хранения пути к ALMAZ_COD.zip
    public string destinationFolderPath;
    public CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    public readonly DownloadService downloadService = new DownloadService();
    public readonly UnzipService unzipService = new UnzipService();
    public readonly DirectoryService directoryService;
    public readonly ConfigurationService installationService;
    public readonly StatusUpdater statusUpdater;

    // Реализация INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;
    public ICommand UnzipACCommand {  get;  } 
    public ICommand DownloadCommand { get; } // Команда для загрузки архива с библиотекой
    public ICommand UnzipBDCommand { get; } // Команда для разархивирования архива библиотеки
    public ICommand InstallationCommand { get; } // Команда для установки программы
    public ICommand SelectFolderCommand { get; } // Команда для выбора папки
    public ICommand CancelCommand { get; } // Команда для отмены

    // Определение класса URL загрузки
    public string DownloadUrl
    {
        get => downloadUrl;
        set
        {
            downloadUrl = value;
            OnPropertyChanged(); 
        }
    }

    // Определение класса пути, указанного пользователем
    public string FolderPath
    {
        get => initialFolderPath;
        set
        {
            initialFolderPath = value;
            OnPropertyChanged(); 
        }
    }
    public string SelectedFilePath
    {
        get => destinationFolderPath; 
        set
        {
            destinationFolderPath = value;
            OnPropertyChanged();
        }
    }

    // Определение класса прогресса
    public int Progress
    {
        get => progress;
        set
        {
            progress = value;
            OnPropertyChanged();
        }
    }

    // Определение класса статуса
    public string Status
    {
        get => status;
        set
        {
            status = value;
            OnPropertyChanged();
        }
    }

    private bool CanUnzipAC()
    {
        // Команда доступна всегда
        return true;
    }
    private bool CanDownload() => !string.IsNullOrWhiteSpace(DownloadUrl); // Проверка возможности выполнения команды загрузки
    private bool CanUnzip() => !string.IsNullOrEmpty(zipPath); // Проверка возможности выполнения команды разархивирования
    private bool CanInstall() => !string.IsNullOrEmpty(FolderPath); // Проверка возможности выполнения команды установки
    private bool CanCancel() => cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested;


    // Конструктор класса модели представления
    public MainWindowViewModel(DownloadService downloadService, UnzipService unzipService, ConfigurationService installationService, DirectoryService directoryService)
    {
        this.downloadService = downloadService;
        this.unzipService = unzipService;
        this.installationService = installationService;
        this.directoryService = directoryService;

        UnzipACCommand = new RelayCommand(async () => await ExecuteUnzipACAsync(), CanUnzipAC);
        DownloadCommand = new RelayCommand(async () => await ExecuteDownloadAsync(), CanDownload); // Инициализация команды загрузки
        UnzipBDCommand = new RelayCommand(async () => await ExecuteUnzipAsync(), CanUnzip); // Инициализация команды разархивирования
        SelectFolderCommand = new RelayCommand(ExecuteSelectFolder);
        CancelCommand = new RelayCommand(ExecuteCancel, CanCancel); // Инициализация команды Cancel
        InstallationCommand = new RelayCommand(async () => await InstallationAsync(), CanInstall); // Инициализация команды Install
        statusUpdater = new StatusUpdater(UpdateStatus);


        UpdateCommands();
    }

    // Асинхронный метод выполнения разархивирования файла ALMAZ_COD
    public async Task ExecuteUnzipACAsync()
    {
        DirectoryService directoryService = new DirectoryService();

        // Открываем диалоговое окно для выбора ZIP-файла
        string zipFilePathAlmaz = directoryService.SelectZipFile(initialFolderPath, UpdateStatus, out string folderPath);

        // Выбор папки назначения для распаковки
        string destinationFolderPath = directoryService.SelectDownloadFolder();

        if (string.IsNullOrEmpty(destinationFolderPath))
        {
            // Если пользователь отменил выбор папки, сообщаем об этом и выходим
            UpdateStatus("Распаковка отменена пользователем.");
            return;
        }

        // Проверяем, был ли выбран файл
        if (string.IsNullOrEmpty(zipFilePathAlmaz))
        {
            return; // Если файл не был выбран, прекращаем выполнение
        }

        // Извлекаем имя файла без расширения
        string archiveName = Path.GetFileNameWithoutExtension(zipFilePathAlmaz);

        // Извлекаем полное имя файла с расширением
        string zipFileName = Path.GetFileName(zipFilePathAlmaz);

        // Путь к распакованной директории
        string almazCodPath = Path.Combine(Path.GetDirectoryName(destinationFolderPath), archiveName);

        if (!File.Exists(zipFilePathAlmaz))
        {
            UpdateStatus($"Файл {zipFileName} не найден.");
            return;
        }

        try
        {
            UpdateStatus($"Распаковка {zipFileName}");

            // Создаем экземпляр службы распаковки и распаковываем выбранный ZIP-файл
            UnzipService unzip = new UnzipService();
            await unzip.UnzipFileAsync(zipFilePathAlmaz, folderPath, almazCodPath, UpdateProgress, UpdateStatus, cancellationTokenSource.Token);

            // Логика после успешной распаковки
            UpdateStatus($"Распаковка {zipFileName} завершена успешно.");
            Progress = 0;
        }
        catch (Exception ex)
        {
            // Обработка ошибок
            UpdateStatus($"Ошибка при распаковке: {ex.Message}");
        }
    }

    public async Task ExecuteDownloadAsync()
    {
        cancellationTokenSource = new CancellationTokenSource();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged(); // Активируем кнопку Cancel

        try
        {
            if (!downloadService.IsValidUrl(DownloadUrl, UpdateStatus))
            {
                UpdateStatus("Некорректный URL.");
                return;
            }
            UpdateStatus("Проверка доступности ссылки...");

            bool isUrlValid = await downloadService.CheckUrlAvailabilityAsync(DownloadUrl, UpdateStatus); // Проверка доступности URL
            if (!isUrlValid)
            {
                UpdateStatus("Ссылка недоступна. Пожалуйста, отредактируйте адрес скачивания."); // Обновление статуса при недоступности URL
                return;
            }

            UpdateStatus("Загрузка...");
            zipPath = await downloadService.DownloadFileAsync(DownloadUrl, UpdateProgress, UpdateStatus, cancellationTokenSource.Token);

            Progress = 0;
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Загрузка была отменена.");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Ошибка при загрузке файла: {ex.Message}");
        }
        finally
        {
            cancellationTokenSource = null;
            UpdateCommands();
            ((RelayCommand)CancelCommand).RaiseCanExecuteChanged(); // Деактивируем кнопку Cancel
        }
    }

    // Асинхронный метод выполнения разархивирования файла
    public async Task ExecuteUnzipAsync()
    {
        if (!string.IsNullOrEmpty(zipPath))
        {
            cancellationTokenSource = new CancellationTokenSource();
            ((RelayCommand)CancelCommand).RaiseCanExecuteChanged(); // Активируем кнопку Cancel
            DirectoryService directoryService = new DirectoryService();
            string initialFolderPath = downloadService.GetLastDownloadFolderPath(); // Получение последнего пути загрузки
            if (string.IsNullOrEmpty(initialFolderPath))
            {
                initialFolderPath = FolderPath; // Использование пути, указанного пользователем, если последний путь не найден
            }
            if (!string.IsNullOrEmpty(initialFolderPath))
            {
                try
                {
                    UpdateStatus("Разархивирование...");
                    // Выбираем папку назначения для распаковки
                    string destinationFolderPath = DirectoryService.SelectDestinationFolder(initialFolderPath);
                    if (string.IsNullOrEmpty(destinationFolderPath))
                    {
                        // Если пользователь отменил выбор папки, сообщаем об этом и выходим
                        UpdateStatus("Распаковка отменена пользователем.");
                        return;
                    }
                    // Разархивирование файла
                    await unzipService.UnzipFileAsync(zipPath, initialFolderPath, destinationFolderPath, UpdateProgress, UpdateStatus, cancellationTokenSource.Token); 

                    FolderPath = initialFolderPath;
                    Progress = 0;
                }
                catch (OperationCanceledException)
                {
                    UpdateStatus("Разархивирование отменено.");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Ошибка при разархивировании: {ex.Message}");
                }
                finally
                {
                    cancellationTokenSource = null;
                    UpdateCommands();
                    ((RelayCommand)CancelCommand).RaiseCanExecuteChanged(); // Деактивируем кнопку Cancel
                }
            }
            else
            {
                UpdateStatus("Путь для извлечения не указан."); // Обновление статуса при отсутствии пути извлечения
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged(); // Деактивируем кнопку Cancel
            }
        }
    }

    public async Task InstallationAsync()
    {
        // Проверяем, указан ли путь к папке
        if (!string.IsNullOrEmpty(initialFolderPath))
        {
            // Инициализация источника токена отмены
            cancellationTokenSource = new CancellationTokenSource();
            ((RelayCommand)CancelCommand).RaiseCanExecuteChanged(); // Деактивируем кнопку Cancel

            //DirectoryService directoryService = new DirectoryService();

            //string initialFolderPath = downloadService.GetLastDownloadFolderPath(); // Получение последнего пути загрузки

            //if (string.IsNullOrEmpty(initialFolderPath))
            //{
            //    initialFolderPath = FolderPath; // Использование пути, указанного пользователем, если последний путь не найден
            //}
            try
            {
                // Обновляем статус, указывая на начало процесса установки
                UpdateStatus("Начало процесса установки...");

                // Выполняем асинхронную установку, передавая необходимые параметры и токен отмены
                await installationService.InstallAsync(almazCodPath,  destinationFolderPath, UpdateProgress, UpdateStatus, cancellationTokenSource.Token);
                Progress = 0;
            }
            catch (Exception ex)
            {
                // Обработка исключений и обновление статуса с сообщением об ошибке
                UpdateStatus($"Ошибка: {ex.Message}");
            }
            finally
            {
                UpdateStatus("Процесс установки завершен.");
                UpdateCommands();
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged(); // Деактивируем кнопку Cancel
            }
        }
        else
        {
            // Если путь к папке не указан, обновляем статус с просьбой выбрать папку
            UpdateStatus("Пожалуйста, выберите папку для установки.");
            ((RelayCommand)CancelCommand).RaiseCanExecuteChanged(); // Деактивируем кнопку Cancel
        }
    }

    private void UpdateCommands()
    {
        // Обновляем состояние всех команд для отражения изменений в состоянии приложения
        ((RelayCommand)DownloadCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UnzipBDCommand).RaiseCanExecuteChanged();
        ((RelayCommand)InstallationCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    private void UpdateProgress(int progressPercentage)
    {
        Progress = progressPercentage; // Обновление прогресса операции
        UpdateStatus($"Прогресс: {progressPercentage}%"); // Обновление статуса с указанием текущего прогресса
    }
    private void ExecuteSelectFolder()
    {
        using (var folderDialog = new WinForm.FolderBrowserDialog())
        {
            WinForm.DialogResult result = folderDialog.ShowDialog(); // Отображение диалогового окна для выбора папки
            if (result == WinForm.DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
            {
                FolderPath = folderDialog.SelectedPath; // Сохранение выбранного пути
                UpdateStatus($"Выбран путь для извлечения: {FolderPath}"); // Обновление статуса с указанием выбранного пути
                UpdateCommands();
            }
        }
    }
    private void ExecuteCancel()
    {
        // Проверяем, можно ли отменить операцию
        if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
        {
            // Отменяем операцию
            cancellationTokenSource.Cancel();
            UpdateStatus("Операция отменена.");
            Progress = 0; // Сброс прогресса
            UpdateCommands();
            ((RelayCommand)CancelCommand).RaiseCanExecuteChanged(); // Деактивируем кнопку Cancel
        }
    }
    private void UpdateStatus(string statusMessage) => Status = statusMessage; // Обновление статуса операции
}
