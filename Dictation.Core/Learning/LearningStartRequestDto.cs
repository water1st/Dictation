using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示按当前计划准备完成的学习启动范围。
    /// </summary>
    public sealed record LearningStartRequestDto(
        LearningLanguage Language,
        int SessionWordLimit,
        LearningSelectionMode SelectionMode,
        IReadOnlyList<string> SelectedWordGroupIds);
}
