using System;
using System.Diagnostics.CodeAnalysis;

namespace Downloader.WPF.Sample
{
    public class Command : DelegateCommandBase
    {
        private Action ExecuteMethod { get; }
        private Func<bool> CanExecuteMethod { get; }

        /// <summary>
        /// Returns a disabled command.
        /// </summary>
        public static Command DisabledCommand { get; } = new Command(() => { }, () => false);

        /// <summary>
        ///     Constructor
        /// </summary>
        public Command([NotNull] Action execute, Func<bool> canExecute = null)
        {
            ExecuteMethod = execute ?? throw new ArgumentNullException(nameof(execute));
            CanExecuteMethod = canExecute;
        }


        /// <inheritdoc />
        public override void Execute(object parameter = null)
        {
            if (!CanExecute())
                return;

            IsExecuting = true;
            try
            {
                ExecuteMethod?.Invoke();
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <inheritdoc />
        public override bool CanExecute(object parameter = null)
        {
            return !IsExecuting && (CanExecuteMethod?.Invoke() ?? true);
        }
    }
}
