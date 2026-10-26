using Dictation.Core.Configuration;

namespace Dictation.Presentation.Navigation
{
    /// <summary>从语言首页进入固定语言工作页。</summary>
    public interface IWorkspaceNavigation
    {
        Task OpenLanguageWorkspaceAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
