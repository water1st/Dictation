using Dictation.Core.Configuration;

namespace Dictation.Presentation.Navigation
{
    /// <summary>只提供错题页面打开原复习练习页的能力，不暴露整个导航栈。</summary>
    public interface IWrongAnswerNavigation
    {
        /// <summary>在同窗口启动或恢复选中复习词组的错误形式练习。</summary>
        /// <param name="language">页面固定的语言。</param>
        /// <param name="wordGroupIds">按整组去重的复习错题选择。</param>
        /// <param name="cancellationToken">取消准备和导航的令牌。</param>
        Task OpenWrongAnswerPracticeAsync(LearningLanguage language, IReadOnlyList<string> wordGroupIds,
            CancellationToken cancellationToken = default);
    }
}
