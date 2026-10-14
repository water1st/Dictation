using System.ComponentModel;
using System.Windows.Input;
using Dictation.Core.Updating;
using Dictation.Presentation.Updating;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>提供非模态更新按钮状态，只有用户选择更新且runner启动成功才请求关闭应用。</summary>
    public sealed class UpdateViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IUpdateReleaseQuery query;
        private readonly IUpdatePreparation preparation;
        private readonly IUpdateLauncher launcher;
        private readonly CancellationTokenSource lifetime = new();
        private readonly AsyncCommand installCommand;
        private AvailableUpdateDto? available;
        private bool isBusy;
        private bool isChecking;
        private bool disposed;
        private string statusMessage = string.Empty;

        /// <summary>组合检查、准备与外部进程启动的最小能力。</summary>
        /// <param name="query">新版检查。</param>
        /// <param name="preparation">完整包下载和runner准备。</param>
        /// <param name="launcher">外部隐藏进程启动。</param>
        public UpdateViewModel(IUpdateReleaseQuery query, IUpdatePreparation preparation, IUpdateLauncher launcher)
        {
            this.query = query ?? throw new ArgumentNullException(nameof(query));
            this.preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            this.launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            installCommand = new AsyncCommand(InstallAsync, () => HasUpdate && !IsBusy && !disposed);
        }

        public bool HasUpdate => available is not null;
        public bool IsBusy => isBusy;
        public string ButtonText => isBusy ? "正在准备更新…" : $"更新到 {available?.Version}";
        public string StatusMessage => statusMessage;
        public ICommand InstallCommand => installCommand;
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? ShutdownRequested;

        /// <summary>启动异步检查，不下载更新包。</summary>
        public async Task CheckAsync()
        {
            if (disposed || isChecking) return;
            isChecking = true;
            var token = lifetime.Token;
            try
            {
                var result = await query.CheckAsync(token);
                // HTTP或替代提供器可能忽略取消，关闭后的结果不能回写UI。
                if (disposed || token.IsCancellationRequested) return;
                available = result;
                statusMessage = string.Empty;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!disposed) statusMessage = $"更新检查未完成：{exception.Message}";
            }
            finally
            {
                isChecking = false;
                if (!disposed) NotifyState();
            }
        }

        /// <summary>处理用户点击，下载校验失败或进程启动失败时保留当前窗口。</summary>
        public async Task InstallAsync()
        {
            if (disposed || isBusy || available is null) return;
            isBusy = true;
            statusMessage = string.Empty;
            NotifyState();
            var token = lifetime.Token;
            try
            {
                var prepared = await preparation.PrepareAsync(available, token);
                token.ThrowIfCancellationRequested();
                if (disposed) return;
                launcher.Launch(prepared);
                ShutdownRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!disposed) statusMessage = $"未能开始更新，可重试：{exception.Message}";
            }
            finally
            {
                isBusy = false;
                if (!disposed) NotifyState();
            }
        }

        /// <summary>关闭时取消进行中的操作并屏蔽晚到响应。</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        /// <summary>统一通知同一更新状态的绑定和命令，不依赖具体页面。</summary>
        private void NotifyState()
        {
            foreach (var name in new[] { nameof(HasUpdate), nameof(IsBusy), nameof(ButtonText), nameof(StatusMessage) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            installCommand.NotifyCanExecuteChanged();
        }
    }
}
