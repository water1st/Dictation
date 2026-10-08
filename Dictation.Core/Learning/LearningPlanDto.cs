using Dictation.Core.Configuration;
using Dictation.Core.Practice;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示指定语言当前生效的学习计划及其展示信息。
    /// </summary>
    public sealed record LearningPlanDto(
        LearningLanguage Language,
        int DailyNewTarget,
        int SessionWordLimit,
        LearningSelectionMode SelectionMode,
        EnglishFormLabelMode EnglishFormLabelMode,
        string? EnglishExampleSourceSenseId,
        IReadOnlyList<string> FormCodes,
        IReadOnlyList<LearningChannel> Channels,
        IReadOnlyList<LearningOutputMode> OutputModes,
        DateTimeOffset UpdatedAtUtc,
        EnglishExampleParadigmDto? EnglishExampleParadigm) : IPracticeQuestionPlan;
}
