using System.Windows.Input;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 将异步 UI 操作适配为防重入的 WPF 命令。
    /// </summary>
    internal sealed class AsyncCommand : ICommand
    {
        private readonly Func<Task> executeAsync;
        private readonly Func<bool>? canExecute;
        private bool isExecuting;

        /// <summary>
        /// 将异步 UI 操作适配为防重入的 WPF 命令。
        /// </summary>
        /// <param name="executeAsync">命令执行时调用的异步 UI 操作。</param>
        /// <param name="canExecute">附加可执行条件；为空时仅使用执行中保护。</param>
        public AsyncCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            this.executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            this.canExecute = canExecute;
        }

        /// <summary>
        /// 检查命令当前是否可执行；异步命令执行中拒绝重入。
        /// </summary>
        /// <param name="parameter">WPF 命令参数；当前实现不使用。</param>
        /// <returns>当前是否允许执行命令。</returns>
        public bool CanExecute(object? parameter)
        {
            return !isExecuting && (canExecute?.Invoke() ?? true);
        }

        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// 通知 WPF 重新计算命令可执行状态。
        /// </summary>
        public void NotifyCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 执行异步操作并在完成后恢复命令状态。
        /// </summary>
        /// <param name="parameter">WPF 命令参数；当前实现不使用。</param>
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
