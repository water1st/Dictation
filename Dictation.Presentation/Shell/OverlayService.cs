using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dictation.Presentation.Shell
{
    public enum OverlayKind
    {
        Message,
        Confirmation,
        Busy
    }

    public sealed class OverlayRequest
    {
        internal OverlayRequest(OverlayKind kind, string title, string message)
        {
            Kind = kind;
            Title = title;
            Message = message;
            Completion = new TaskCompletionSource<bool?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public OverlayKind Kind { get; }

        public string Title { get; }

        public string Message { get; }

        internal TaskCompletionSource<bool?> Completion { get; }

        internal CancellationTokenRegistration CancellationRegistration { get; set; }
    }

    public sealed class OverlayService : INotifyPropertyChanged, IDisposable
    {
        private OverlayRequest? currentRequest;
        private bool isDisposed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public OverlayRequest? CurrentRequest => currentRequest;

        public bool IsVisible => currentRequest is not null;

        public bool IsBusy => currentRequest?.Kind == OverlayKind.Busy;

        public bool IsConfirmation => currentRequest?.Kind == OverlayKind.Confirmation;

        public Task ShowMessageAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            return AwaitMessageAsync(ShowAsync(OverlayKind.Message, title, message, cancellationToken));
        }

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            return AwaitConfirmationAsync(
                ShowAsync(OverlayKind.Confirmation, title, message, cancellationToken));
        }

        public Task ShowBusyAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            return AwaitMessageAsync(ShowAsync(OverlayKind.Busy, title, message, cancellationToken));
        }

        public void Accept()
        {
            Complete(true);
        }

        public void Cancel()
        {
            Complete(false);
        }

        public void Dismiss()
        {
            Complete(null);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            Complete(null);
        }

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
