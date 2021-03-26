using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Downloader.WPF.Sample
{
    public class CommandAsync : DelegateCommandBase
    {
        private Func<Task> ExecuteMethod { get; }
        private Func<bool> CanExecuteMethod { get; }

        /// <summary>
        /// Returns a disabled command.
        /// </summary>
        public static CommandAsync DisabledCommand { get; } = new CommandAsync(() => CompletedTask, () => false);

        /// <summary>
        ///     Constructor
        /// </summary>
        public CommandAsync([NotNull] Func<Task> execute, Func<bool> canExecute = null)
        {
            ExecuteMethod = execute ?? throw new ArgumentNullException(nameof(execute));
            CanExecuteMethod = canExecute;
        }

        /// <inheritdoc />
        public override bool CanExecute(object parameter = null)
        {
            return !IsExecuting && (CanExecuteMethod?.Invoke() ?? true);
        }

        /// <inheritdoc />
        public override void Execute(object parameter = null)
        {
            ExecuteAsync();
        }
        private async Task ExecuteAsync()
        {
            if (CanExecute())
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

            RaiseCanExecuteChanged();
        }
    }
}
