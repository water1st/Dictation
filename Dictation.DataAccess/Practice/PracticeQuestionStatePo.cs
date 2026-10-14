namespace Dictation.DataAccess.Practice
{
    /// <summary>
    /// 表示当前持久化待答题的定位字段。
    /// </summary>
    public sealed record PracticeQuestionStatePo(
        long QuestionId,
        string SessionId,
        string WordGroupId,
        long MemberId,
        string FormCode,
        string Channel,
        string OutputMode,
        int QuestionOrdinal);
}
