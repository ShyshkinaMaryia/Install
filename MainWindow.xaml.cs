using System.Windows;

namespace Install
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Создание экземпляров служб для загрузки, распаковки и установки
            var downloadService = new DownloadService();
            var unzipService = new UnzipService();
            var installationService = new ConfigurationService();
            DirectoryService directoryService = new DirectoryService();

            // Связываем модель представления с пользовательским интерфейсом
            DataContext = new MainWindowViewModel(downloadService, unzipService, installationService, directoryService);
        }
    }
}
