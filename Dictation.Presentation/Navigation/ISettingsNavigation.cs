namespace Dictation.Presentation.Navigation
{
    /// <summary>从首页或语言工作页打开设置宿主。</summary>
    public interface ISettingsNavigation
    {
        /// <summary>打开设置并保留发起页面，转场及语言选择期间忽略重复请求。</summary>
        /// <param name="cancellationToken">取消本次导航。</param>
        Task OpenSettingsAsync(CancellationToken cancellationToken = default);
    }
}
