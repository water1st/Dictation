using System.ComponentModel;

namespace Dictation.Presentation.Navigation
{
    /// <summary>
    /// 向单窗口宿主公开当前导航状态、返回操作与转场完成边界。
    /// </summary>
    public interface INavigationHost : INotifyPropertyChanged
    {
        /// <summary>
        /// 页面切换后请求宿主播放转场动画。
        /// </summary>
        event EventHandler<NavigationTransitionEventArgs>? TransitionRequested;

        /// <summary>
        /// 获取当前由单窗口宿主呈现的页面视图模型。
        /// </summary>
        object? CurrentPage { get; }

        /// <summary>
        /// 获取当前页面标题。
        /// </summary>
        string PageTitle { get; }

        /// <summary>
        /// 获取当前页面是否允许返回上一页。
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// 返回上一页；离开页面会延迟到宿主报告转场完成后释放。
        /// </summary>
        Task GoBackAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 通知导航状态机转场动画结束，并释放等待中的页面作用域。
        /// </summary>
        void CompleteTransition();
    }
}
