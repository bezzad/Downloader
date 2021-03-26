using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Downloader.WPF.Sample
{
    public abstract class DelegateCommandBase : INotifyPropertyChanged, ICommand
    {
        protected static event EventHandler RequerySuggested = delegate { };
        protected event EventHandler InternalCanExecuteChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add
            {
                InternalCanExecuteChanged += value;
                RequerySuggested += value;
            }
            remove
            {
                InternalCanExecuteChanged -= value;
                RequerySuggested -= value;
            }
        }


        private bool _isExecuting;
        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    RaiseCanExecuteChanged();
                }
            }
        }
        protected static Task<object> CompletedTask { get; } = Task.FromResult((object)null);

        /// <summary>
        /// This method can be used to raise the CanExecuteChanged handler.
        /// This will force WPF to re-query the status of this command directly.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            OnCanExecuteChanged();
            InvalidateRequerySuggested();
        }

        /// <summary>
        /// This method is used to walk the delegate chain and well WPF that
        /// our command execution status has changed.
        /// </summary>
        private void OnCanExecuteChanged()
        {
            InternalCanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to null.</param>
        /// <returns>true if this command can be executed; otherwise, false.</returns>
        public abstract bool CanExecute(object parameter);

        /// <summary>
        /// Defines the method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to null.</param>
        public abstract void Execute(object parameter);

        public static void InvalidateRequerySuggested()
        {
            RequerySuggested(null, EventArgs.Empty);
        }

        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
