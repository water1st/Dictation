using Dictation.Core.Configuration;

namespace Dictation.Presentation.Navigation
{
    /// <summary>
    /// 提供首页可发起的顶层页面导航能力。
    /// </summary>
    public interface IHomeNavigation
    {
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
    }
}
