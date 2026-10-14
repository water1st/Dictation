namespace Dictation.Presentation.Navigation
{
    /// <summary>提供五十音计划页所需的独立听写导航能力。</summary>
    public interface IKanaSessionNavigation
    {
        /// <summary>在同一窗口从五十音计划进入或恢复原听写轮次。</summary>
        /// <param name="cancellationToken">取消导航及首题准备。</param>
        Task OpenKanaSessionAsync(CancellationToken cancellationToken = default);
    }
}
