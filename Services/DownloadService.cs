using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

public class DownloadService
{
    public string lastDownloadFolderPath; // Переменная для хранения пути последней папки загрузки
    private readonly DirectoryService directoryService;

    public DownloadService()
    {
        this.directoryService = new DirectoryService();
    }

    // Асинхронный метод для загрузки файла
    public async Task<string> DownloadFileAsync(string url, Action<int> onProgressChanged, Action<string> onStatusChanged, CancellationToken cancellationToken)
    {
        try
        {
            // Получаем имя файла из URL
            var fileName = Path.GetFileName(url);

            lastDownloadFolderPath = directoryService.SelectDownloadFolder();

            if (string.IsNullOrEmpty(lastDownloadFolderPath))
            {
                // Если пользователь отменил выбор папки, сообщаем об этом и выходим
                onStatusChanged("Загрузка отменена пользователем.");
                return string.Empty;
            }

            // Полный путь к файлу для сохранения
            var filePath = Path.Combine(lastDownloadFolderPath, fileName);

            using (var client = new WebClient())
            {
                // Обработчик события изменения прогресса загрузки
                client.DownloadProgressChanged += (s, e) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Если запрос на отмену, отменяем загрузку
                        client.CancelAsync();
                        return;
                    }
                    // Обновляем прогресс загрузки
                    onProgressChanged(e.ProgressPercentage);
                };

                cancellationToken.Register(() => client.CancelAsync());

                try
                {
                    // Асинхронная загрузка файла
                    await client.DownloadFileTaskAsync(new Uri(url), filePath);
                    onStatusChanged("Загрузка завершена.");
                    return filePath;
                }
                catch (WebException ex) when (ex.Status == WebExceptionStatus.RequestCanceled)
                {
                    // Обработка отмены загрузки пользователем
                    onStatusChanged("Загрузка отменена пользователем.");
                    return string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            // Сообщаем об ошибке, если возникло исключение при загрузке файла
            onStatusChanged($"Ошибка при загрузке файла: {ex.Message}");
            return string.Empty;
        }
    }

    // Асинхронный метод для проверки доступности URL
    public async Task<bool> CheckUrlAvailabilityAsync(string url, Action<string> onStatusChanged)
    {
        try
        {
            // Создаем запрос к указанному URL
            var request = WebRequest.Create(url);
            // Устанавливаем метод запроса как HEAD, чтобы получить только заголовки
            request.Method = "HEAD";

            // Выполняем асинхронный запрос и ждем ответа
            var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(
                request.BeginGetResponse,
                request.EndGetResponse,
                null
            );

            // Проверяем, вернул ли сервер статус OK (200)
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return true; // URL доступен
            }

            return false; // URL недоступен
        }
        catch (Exception ex)
        {
            // Сообщаем об ошибке, если возникло исключение
            onStatusChanged($"Ошибка при проверке URL: {ex.Message}");
            return false;
        }
    }

    // Метод для проверки корректности URL
    public bool IsValidUrl(string url, Action<string> onStatusChanged)
    {
        // Проверяем, что URL не пустой или не состоит только из пробелов
        if (string.IsNullOrWhiteSpace(url))
        {
            onStatusChanged("URL не может быть пустым.");
            return false; // URL некорректен
        }

        // Пытаемся создать объект Uri и проверяем, что схема является HTTP или HTTPS
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) ||
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
        {
            onStatusChanged("Некорректный URL. Пожалуйста, введите корректный адрес.");
            return false; // URL некорректен
        }

        return true; // URL корректен
    }

    public string GetLastDownloadFolderPath()
    {
        return lastDownloadFolderPath;
    }
}