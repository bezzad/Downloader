using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Downloader.WPF.Sample
{
    public sealed class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private double _progressValue;
        private double _progressMaximumValue;
        private string _urlAddress;
        private string _filePath;
        private string _infoText;
        private int _chunksCount;
        private bool _downloadOnTheFly;
        private bool _parallelDownload;
        private long _speed;
        private bool _isReady;

        public bool IsReady
        {
            get => _isReady;
            set
            {
                _isReady = value;
                OnPropertyChanged(nameof(IsReady));
            }
        }

        public long Speed
        {
            get => _speed;
            set
            {
                _speed = value;
                OnPropertyChanged(nameof(Speed));
            }
        }

        public bool ParallelDownload
        {
            get => _parallelDownload;
            set
            {
                _parallelDownload = value;
                OnPropertyChanged(nameof(ParallelDownload));
            }
        }

        public bool DownloadOnTheFly
        {
            get => _downloadOnTheFly;
            set
            {
                _downloadOnTheFly = value;
                OnPropertyChanged(nameof(DownloadOnTheFly));
            }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        public int ChunksCount
        {
            get => _chunksCount;
            set
            {
                _chunksCount = value > 0 ? value : 1;
                OnPropertyChanged(nameof(ChunksCount));
            }
        }

        public double ProgressMaximumValue
        {
            get => _progressMaximumValue;
            set
            {
                _progressMaximumValue = value;
                OnPropertyChanged(nameof(ProgressMaximumValue));
            }
        }

        public string UrlAddress
        {
            get => _urlAddress;
            set
            {
                _urlAddress = value;
                OnPropertyChanged(nameof(UrlAddress));
            }
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged(nameof(FilePath));
            }
        }

        public string InfoText
        {
            get => _infoText;
            set
            {
                _infoText = value;
                OnPropertyChanged(nameof(InfoText));
            }
        }

        public ICommand StartCommand { get; set; }
        public ICommand StopCommand { get; set; }
        public ICommand SavePackageCommand { get; set; }
        public ICommand OpenPackageCommand { get; set; }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
