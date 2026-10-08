using Dictation.Core.Configuration;

namespace Dictation.Presentation.Navigation
{
    /// <summary>
    /// 提供首页可发起的顶层页面导航能力。
    /// </summary>
    public interface IHomeNavigation
    {
        /// <summary>在同一窗口打开固定语言的复习会话。</summary>
        /// <param name="language">首页选定的语言。</param>
        /// <param name="cancellationToken">取消导航和准备的令牌。</param>
        Task OpenReviewSessionAsync(LearningLanguage language, CancellationToken cancellationToken = default);

        /// <summary>打开固定语言的独立复习计划与整组管理页面。</summary>
        /// <param name="language">首页选定的语言。</param>
        /// <param name="cancellationToken">取消导航和读取的令牌。</param>
        Task OpenReviewPlanAsync(LearningLanguage language, CancellationToken cancellationToken = default);

        /// <summary>打开固定语言的只读复习词库页面。</summary>
        /// <param name="language">首页选定语言。</param>
        /// <param name="cancellationToken">取消导航和首轮加载的令牌。</param>
        Task OpenReviewLibraryAsync(LearningLanguage language, CancellationToken cancellationToken = default);

        /// <summary>打开固定语言的文本词表导入页面。</summary>
        /// <param name="language">首页选定的学习语言。</param>
        /// <param name="initialFilePath">可选的首次加载 TXT 路径。</param>
        /// <param name="cancellationToken">取消导航的令牌。</param>
        Task OpenTextImportAsync(
            LearningLanguage language,
            string? initialFilePath = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 打开固定语言的查词页面。
        /// </summary>
        Task OpenLexiconLookupAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 打开固定语言的导入收件箱。
        /// </summary>
        Task OpenImportInboxAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 打开固定语言的学习计划页面。
        /// </summary>
        Task OpenLearningPlanAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 打开固定语言的学习会话。
        /// </summary>
        Task OpenLearningAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default);
    }
}
