using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示启动或恢复学习会话的输入。
    /// </summary>
    public sealed record StartLearningSessionRequestDto(
        LearningLanguage Language,
        IReadOnlyList<string>? SelectedWordGroupIds = null);
}
