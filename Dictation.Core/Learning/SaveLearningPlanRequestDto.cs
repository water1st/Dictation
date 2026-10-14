using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示保存学习计划时提交的全部选项。
    /// </summary>
    public sealed record SaveLearningPlanRequestDto(
        LearningLanguage Language,
        int DailyNewTarget,
        int SessionWordLimit,
        LearningSelectionMode SelectionMode,
        EnglishFormLabelMode EnglishFormLabelMode,
        string? EnglishExampleSourceSenseId,
        IReadOnlyList<string> FormCodes,
        IReadOnlyList<LearningChannel> Channels,
        IReadOnlyList<LearningOutputMode> OutputModes);
}
