using System.ComponentModel;

namespace Dictation.Presentation.Shell
{
    /// <summary>
    /// 向单窗口宿主公开弹层状态与当前请求的完成操作。
    /// </summary>
    public interface IOverlayHost : INotifyPropertyChanged
    {
        OverlayRequest? CurrentRequest { get; }

        bool IsVisible { get; }

        bool IsBusy { get; }

        bool IsConfirmation { get; }

        void Accept();

        void Cancel();

        void Dismiss();
    }
}
