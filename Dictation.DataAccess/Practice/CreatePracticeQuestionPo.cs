namespace Dictation.DataAccess.Practice
{
    /// <summary>
    /// 表示创建当前待答题的持久化输入。
    /// </summary>
    public sealed record CreatePracticeQuestionPo(
        string SessionId,
        string WordGroupId,
        long MemberId,
        string FormCode,
        string Channel,
        string OutputMode,
        int QuestionOrdinal,
        DateTimeOffset CreatedAtUtc);
}
