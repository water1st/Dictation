using System.Windows.Input;

namespace Dictation.Presentation.ViewModels
{
    internal sealed class AsyncCommand : ICommand
    {
        private readonly Func<Task> executeAsync;
        private bool isExecuting;

        public AsyncCommand(Func<Task> executeAsync)
        {
            this.executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        }

        public bool CanExecute(object? parameter)
        {
            return !isExecuting;
        }

        public event EventHandler? CanExecuteChanged;

        public async void Execute(object? parameter)
        {
            if (isExecuting)
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
