using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;

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
                IsReady = true,
                ProgressMaximumValue = 100,
                ProgressValue = 0,
                ChunksCount = 1,
                DownloadOnTheFly = false,
                UrlAddress = "https://file-examples-com.github.io/uploads/2017/11/file_example_MP3_5MG.mp3",
                FilePath = "D:\\sample.mp3",
                InfoText = "Download info...",
                Speed = 1024 * 256, // 256 KB/s
                StartCommand = new CommandAsync(OnStartDownload, () => _downloader?.IsBusy != true),
                StopCommand = new Command(OnStopDownload, () => _downloader?.IsBusy == true),
                SavePackageCommand = new Command(OnSavePackage, () => _downloader != null),
                OpenPackageCommand = new Command(OnOpenPackage, () => _downloader?.IsBusy != true)
            };
            DataContext = Model;

            Title += typeof(DownloadService).Assembly.GetName().Version?.ToString(3);
        }

        private void OnOpenPackage()
        {
            var openFileDialog = new OpenFileDialog() {
                FileName = "download.package",
                Filter = "Package files (*.package)|*.package|All files (*.*)|*.*",
                Title = "Open download package"
            };

            if (openFileDialog.ShowDialog(this) == true)
            {
                Model.UrlAddress = openFileDialog.FileName;
            }
        }

        private void OnSavePackage()
        {
            var saveFileDialog = new SaveFileDialog() {
                FileName = "download.package",
                Filter = "Package files (*.package)|*.package|All files (*.*)|*.*",
                Title = "Store download package"
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
            Model.IsReady = true;
        }

        private async Task OnStartDownload()
        {
            Model.InfoText = "Starting...";
            Model.IsReady = false;
            InitDownloader();

            if (Model.UrlAddress.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                await _downloader.DownloadFileTaskAsync(Model.UrlAddress, Model.FilePath);
            }
            else
            {
                IFormatter formatter = new BinaryFormatter();
                await using var packageStream = File.OpenRead(Model.UrlAddress);
                var package = formatter.Deserialize(packageStream) as DownloadPackage;
                await _downloader.DownloadFileTaskAsync(package);
            }
        }

        private void InitDownloader()
        {
            var config = new DownloadConfiguration() {
                ChunkCount = Model.ChunksCount,
                ParallelDownload = Model.ParallelDownload,
                OnTheFlyDownload = Model.DownloadOnTheFly,
                MaximumBytesPerSecond = Model.Speed,
                CheckDiskSizeBeforeDownload = true
            };
            _downloader?.Dispose();
            _downloader = new DownloadService(config);
            _downloader.DownloadStarted += OnDownloadStarted;
            _downloader.DownloadProgressChanged += OnProgressChanged;
            _downloader.DownloadFileCompleted += OnDownloadCompleted;
        }

        private void OnDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                Model.InfoText = "Download Cancelled!";
            }
            else if (e.Error != null)
            {
                Model.InfoText = "Error: " + e.Error.Message;
            }
            else
            {
                Model.InfoText = "Download Completed Successfully.";
            }
        }

        private void OnProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Model.ProgressValue = e.ProgressPercentage;
            UpdateDownloadInfo(_downloader.Package);
            Dispatcher.Invoke(() => {
                TaskbarItemInfo ??= new TaskbarItemInfo();
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                TaskbarItemInfo.ProgressValue = e.ProgressPercentage / 100;
            });
        }

        private void UpdateDownloadInfo(DownloadPackage package)
        {
            var info = "";
            for (var i = 0; i < package.Chunks.Length; i++)
            {
                Chunk chunk = package.Chunks[i];
                if (chunk.Storage is FileStorage fileStorage)
                {
                    info += $"Chunk[{i}]: {fileStorage.FileName} {fileStorage.GetLength()}bytes \n\r";
                }
                else if (chunk.Storage is MemoryStorage memoryStorage)
                {
                    info += $"Chunk[{i}]: {memoryStorage.GetLength()}bytes \n\r";
                }
            }

            Model.InfoText = info;
        }

        private void OnDownloadStarted(object sender, DownloadStartedEventArgs e)
        {
            Model.FilePath = e.FileName;
            Dispatcher.Invoke(DelegateCommandBase.InvalidateRequerySuggested);
        }
    }
}
