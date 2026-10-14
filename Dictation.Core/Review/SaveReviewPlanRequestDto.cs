using Dictation.Core.Configuration;
using Dictation.Core.Learning;

namespace Dictation.Core.Review
{
    /// <summary>提交独立复习计划及整组范围；范围选择不会删除未选词组。</summary>
    public sealed record SaveReviewPlanRequestDto(
        LearningLanguage Language,
        int DailyReviewTarget,
        int SessionWordLimit,
        ReviewRangeMode RangeMode,
        IReadOnlyList<string> WordGroupIds,
        IReadOnlyList<string> FormCodes,
        IReadOnlyList<LearningChannel> Channels,
        IReadOnlyList<LearningOutputMode> OutputModes,
        EnglishFormLabelMode EnglishFormLabelMode,
        string? EnglishExampleSourceSenseId);
}
