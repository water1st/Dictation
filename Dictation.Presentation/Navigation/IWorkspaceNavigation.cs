using Dictation.Core.Configuration;

namespace Dictation.Presentation.Navigation
{
    /// <summary>从语言首页进入固定语言工作页。</summary>
    public interface IWorkspaceNavigation
    {
        /// <summary>仅从根首页进入已保存语言的工作页。</summary>
        /// <param name="language">已确认的学习语言。</param>
        /// <param name="cancellationToken">取消本次导航。</param>
        Task OpenLanguageWorkspaceAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
