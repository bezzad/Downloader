using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Downloader.WPF.Sample
{
    public class CommandAsync : INotifyPropertyChanged, ICommand
    {
        private bool _isExecuting;
        private Func<Task> ExecuteMethod { get; }
        private Func<bool> CanExecuteMethod { get; }
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler CanExecuteChanged;
        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    OnCanExecuteChanged();
                }
            }
        }

        public CommandAsync([NotNull] Func<Task> execute, Func<bool> canExecute = null)
        {
            ExecuteMethod = execute ?? throw new ArgumentNullException(nameof(execute));
            CanExecuteMethod = canExecute;
        }

        public bool CanExecute(object parameter = null)
        {
            return !IsExecuting && (CanExecuteMethod?.Invoke() ?? true);
        }

        public void Execute(object parameter = null)
        {
            ExecuteAsync();
        }
        private async Task ExecuteAsync()
        {
            if (CanExecute(null))
            {
                try
                {
                    IsExecuting = true;
                    await ExecuteMethod();
                }
                finally
                {
                    IsExecuting = false;
                }
            }

            OnCanExecuteChanged();
        }

        private void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
