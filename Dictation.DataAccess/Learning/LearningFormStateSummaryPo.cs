namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示候选词形已有进度的持久化摘要。
    /// </summary>
    public sealed record LearningFormStateSummaryPo(
        long MemberId,
        string FormCode,
        bool IsMastered);
}
