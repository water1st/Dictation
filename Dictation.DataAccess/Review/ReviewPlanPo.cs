namespace Dictation.DataAccess.Review
{
    /// <summary>保存按语言独立的复习计划字段及有序关联选项。</summary>
    public sealed record ReviewPlanPo(
        string Language,
        int DailyReviewTarget,
        int SessionWordLimit,
        string RangeMode,
        IReadOnlyList<string> WordGroupIds,
        IReadOnlyList<string> FormCodes,
        IReadOnlyList<string> Channels,
        IReadOnlyList<string> OutputModes,
        string EnglishFormLabelMode,
        string? EnglishExampleSourceSenseId,
        DateTimeOffset UpdatedAtUtc);
}
