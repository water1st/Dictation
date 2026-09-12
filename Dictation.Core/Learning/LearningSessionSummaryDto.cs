namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示已完成学习会话的答题汇总。
    /// </summary>
    public sealed record LearningSessionSummaryDto(
        string SessionId,
        int AnsweredCount,
        IReadOnlyList<LearningErrorSummaryDto> Errors);
}
