namespace Dictation.Presentation.Shell
{
    /// <summary>
    /// 描述宿主正在显示的一次弹层请求及其异步完成状态。
    /// </summary>
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
}
