namespace Dictation.Presentation.Navigation
{
    /// <summary>从当前页面打开设置宿主。</summary>
    public interface ISettingsNavigation
    {
        /// <summary>保留发起页面并打开设置；设置页内重入选择外观，转场及语言选择期间忽略重复请求。</summary>
        /// <param name="cancellationToken">取消本次导航。</param>
        Task OpenSettingsAsync(CancellationToken cancellationToken = default);
    }
}
