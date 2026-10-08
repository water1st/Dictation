namespace Dictation.Presentation.Practice
{
    /// <summary>显示本轮原始错误；学习合同没有成员标识时保持空，不编造关联。</summary>
    public sealed record PracticeErrorSummary(string WordGroupId, long? MemberId, string FormCode, string CorrectAnswer);
}
