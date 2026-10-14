using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dictation.Presentation.Shell
{
    /// <summary>
    /// 在单窗口内串行管理弹层请求，并向页面与宿主暴露彼此隔离的能力。
    /// </summary>
    internal sealed class OverlayService : IOverlayRequester, IOverlayHost, IDisposable
    {
        private OverlayRequest? currentRequest;
        private bool isDisposed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public OverlayRequest? CurrentRequest => currentRequest;

        public bool IsVisible => currentRequest is not null;

        public bool IsBusy => currentRequest?.Kind == OverlayKind.Busy;

        public bool IsConfirmation => currentRequest?.Kind == OverlayKind.Confirmation;

        /// <inheritdoc />
        public Task ShowMessageAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            return AwaitMessageAsync(ShowAsync(OverlayKind.Message, title, message, cancellationToken));
        }

        /// <inheritdoc />
        public Task<bool> ConfirmAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            return AwaitConfirmationAsync(
                ShowAsync(OverlayKind.Confirmation, title, message, cancellationToken));
        }

        /// <inheritdoc />
        public Task ShowBusyAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            return AwaitMessageAsync(ShowAsync(OverlayKind.Busy, title, message, cancellationToken));
        }

        /// <inheritdoc />
        public void Accept()
        {
            Complete(true);
        }

        /// <inheritdoc />
        public void Cancel()
        {
            Complete(false);
        }

        /// <inheritdoc />
        public void Dismiss()
        {
            Complete(null);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            Complete(null);
        }

        /// <summary>
        /// 建立唯一在途弹层请求和取消订阅；已有弹层时拒绝并发显示。
        /// </summary>
        private Task<bool?> ShowAsync(
            OverlayKind kind,
            string title,
            string message,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            cancellationToken.ThrowIfCancellationRequested();
            if (currentRequest is not null)
            {
                throw new InvalidOperationException("已有弹层正在显示。");
            }

            var request = new OverlayRequest(kind, title.Trim(), message.Trim());
            currentRequest = request;
            if (cancellationToken.CanBeCanceled)
            {
                // 取消回调可能在线程池线程执行；这里只完成请求状态，不直接访问 WPF 控件。
                request.CancellationRegistration = cancellationToken.Register(
                    static state => ((OverlayService)state!).Dismiss(),
                    this);
            }

            OnPropertyChanged(nameof(CurrentRequest));
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsConfirmation));
            return request.Completion.Task;
        }

        private void Complete(bool? result)
        {
            var request = currentRequest;
            if (request is null)
            {
                return;
            }

            currentRequest = null;
            request.CancellationRegistration.Dispose();
            request.Completion.TrySetResult(result);
            OnPropertyChanged(nameof(CurrentRequest));
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsConfirmation));
        }

        private static async Task AwaitMessageAsync(Task<bool?> completion)
        {
            await completion.ConfigureAwait(false);
        }

        private static async Task<bool> AwaitConfirmationAsync(Task<bool?> completion)
        {
            return (await completion.ConfigureAwait(false)) == true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
