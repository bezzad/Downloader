using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace Downloader.WPF.Sample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DownloadService _downloader;
        public Model Model { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Model = new Model() {
                ProgressMaximumValue = 100,
                ProgressValue = 10,
                UrlAddress = "URL or Package Address",
                FilePath = "File Storage Path",
                InfoText = "Download info...",
                StartCommand = new CommandAsync(OnStartDownload, () => _downloader?.IsBusy != true),
                StopCommand = new Command(OnStopDownload, () => _downloader?.IsBusy == true),
                SaveCommand = new Command(OnSavePackage, () => _downloader != null)
            };
            DataContext = Model;

            Title += typeof(DownloadService).Assembly.GetName().Version?.ToString(3);
        }

        private void OnSavePackage()
        {
            var saveFileDialog = new SaveFileDialog() {
                FileName = "download.package", Filter = "*.package", Title = "Store download package"
            };

            if (saveFileDialog.ShowDialog(this) == true)
            {
                IFormatter formatter = new BinaryFormatter();
                using var serializedStream = File.Create(saveFileDialog.FileName);
                formatter.Serialize(serializedStream, _downloader.Package);
            }
        }

        private void OnStopDownload()
        {
            _downloader?.CancelAsync();
        }

        private async Task OnStartDownload()
        {
            _downloader = new DownloadService();

            if (Model.UrlAddress.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                await _downloader.DownloadFileTaskAsync(Model.UrlAddress, Model.FilePath);
            }
            else
            {
                IFormatter formatter = new BinaryFormatter();
                var packageStream = File.OpenRead(Model.UrlAddress);
                var package = formatter.Deserialize(packageStream) as DownloadPackage;
                await _downloader.DownloadFileTaskAsync(package);
            }
        }
    }
}
