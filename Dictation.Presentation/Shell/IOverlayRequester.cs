namespace Dictation.Presentation.Shell
{
    /// <summary>
    /// 提供页面请求消息、确认和忙碌弹层的能力。
    /// </summary>
    public interface IOverlayRequester
    {
        Task ShowMessageAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default);

        Task<bool> ConfirmAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default);

        Task ShowBusyAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default);
    }
}
