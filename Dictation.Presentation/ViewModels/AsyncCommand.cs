using System.Windows.Input;

namespace Dictation.Presentation.ViewModels
{
    internal sealed class AsyncCommand : ICommand
    {
        private readonly Func<Task> executeAsync;
        private readonly Func<bool>? canExecute;
        private bool isExecuting;

        public AsyncCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            this.executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return !isExecuting && (canExecute?.Invoke() ?? true);
        }

        public event EventHandler? CanExecuteChanged;

        public void NotifyCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            isExecuting = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                await executeAsync();
            }
            finally
            {
                isExecuting = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
