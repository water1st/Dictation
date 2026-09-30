using Dictation.Core.Configuration;
using Dictation.Core.Learning;

namespace Dictation.Core.Review
{
    /// <summary>表示独立保存的复习计划和按需解析的英语标签范式。</summary>
    public sealed record ReviewPlanDto(
        LearningLanguage Language,
        int DailyReviewTarget,
        int SessionWordLimit,
        ReviewRangeMode RangeMode,
        IReadOnlyList<string> WordGroupIds,
        IReadOnlyList<string> FormCodes,
        IReadOnlyList<LearningChannel> Channels,
        IReadOnlyList<LearningOutputMode> OutputModes,
        EnglishFormLabelMode EnglishFormLabelMode,
        string? EnglishExampleSourceSenseId,
        DateTimeOffset UpdatedAtUtc,
        EnglishExampleParadigmDto? EnglishExampleParadigm);
}
