namespace Dictation.Presentation.Shell
{
    /// <summary>
    /// 提供页面请求消息、确认和忙碌弹层的能力。
    /// </summary>
    public interface IOverlayRequester
    {
        /// <summary>
        /// 显示消息并等待用户关闭。
        /// </summary>
        Task ShowMessageAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 显示确认请求并返回用户选择。
        /// </summary>
        Task<bool> ConfirmAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 显示忙碌状态，直到请求被取消或宿主关闭。
        /// </summary>
        Task ShowBusyAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default);
    }
}
