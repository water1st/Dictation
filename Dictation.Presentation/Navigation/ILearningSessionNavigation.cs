using Dictation.Core.Configuration;

namespace Dictation.Presentation.Navigation
{
    /// <summary>
    /// 在当前单窗口中打开固定语言和选词范围的学习会话。
    /// </summary>
    public interface ILearningSessionNavigation
    {
        /// <summary>
        /// 进入学习会话，并把手动选择的完整词组交给会话启动流程。
        /// </summary>
        /// <param name="language">本轮固定的学习语言。</param>
        /// <param name="selectedWordGroupIds">手动模式选中的词组；自动模式可为空。</param>
        /// <param name="cancellationToken">取消页面导航和会话准备的令牌。</param>
        Task OpenLearningSessionAsync(
            LearningLanguage language,
            IReadOnlyList<string>? selectedWordGroupIds,
            CancellationToken cancellationToken = default);
    }
}
