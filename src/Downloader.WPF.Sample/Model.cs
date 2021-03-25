using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Downloader.WPF.Sample
{
    public class Model : INotifyPropertyChanged
    {
        private double _progressValue;
        private double _progressMaximumValue;
        private string _urlAddress;
        private string _filePath;
        private string _infoText;

        public event PropertyChangedEventHandler PropertyChanged;
        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
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
        public ICommand SaveCommand { get; set; }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
