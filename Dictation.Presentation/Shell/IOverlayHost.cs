using System.ComponentModel;

namespace Dictation.Presentation.Shell
{
    /// <summary>
    /// 向单窗口宿主公开弹层状态与当前请求的完成操作。
    /// </summary>
    public interface IOverlayHost : INotifyPropertyChanged
    {
        /// <summary>
        /// 获取当前弹层请求；没有活动请求时为 <see langword="null"/>。
        /// </summary>
        OverlayRequest? CurrentRequest { get; }

        /// <summary>
        /// 获取当前是否显示弹层。
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// 获取当前弹层是否为忙碌状态。
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// 获取当前弹层是否要求确认。
        /// </summary>
        bool IsConfirmation { get; }

        /// <summary>
        /// 接受当前确认请求。
        /// </summary>
        void Accept();

        /// <summary>
        /// 取消当前确认请求。
        /// </summary>
        void Cancel();

        /// <summary>
        /// 关闭当前消息或忙碌请求。
        /// </summary>
        void Dismiss();
    }
}
