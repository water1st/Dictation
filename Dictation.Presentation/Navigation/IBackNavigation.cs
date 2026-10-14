namespace Dictation.Presentation.Navigation
{
    /// <summary>
    /// 提供功能页面返回上一页的导航能力。
    /// </summary>
    public interface IBackNavigation
    {
        /// <summary>
        /// 在允许返回时恢复上一页，并等待宿主完成转场后释放离开页面。
        /// </summary>
        Task GoBackAsync(CancellationToken cancellationToken = default);
    }
}
