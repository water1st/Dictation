namespace Dictation.DataAccess.Practice
{
    /// <summary>
    /// 表示会话内一道题及其答题结果的持久化历史。
    /// </summary>
    public sealed record PracticeQuestionHistoryPo(
        long QuestionId,
        string WordGroupId,
        long MemberId,
        string FormCode,
        string Channel,
        string OutputMode,
        int QuestionOrdinal,
        bool? WasCorrect);
}
