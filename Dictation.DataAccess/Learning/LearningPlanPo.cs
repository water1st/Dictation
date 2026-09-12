namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示持久化的学习计划字段。
    /// </summary>
    public sealed record LearningPlanPo(
        string Language,
        int DailyNewTarget,
        int SessionWordLimit,
        string SelectionMode,
        string EnglishFormLabelMode,
        string? EnglishExampleSourceSenseId,
        IReadOnlyList<string> FormCodes,
        IReadOnlyList<string> Channels,
        IReadOnlyList<string> OutputModes,
        DateTimeOffset UpdatedAtUtc);
}
