namespace Dictation.Presentation.Practice
{
    /// <summary>显示本轮原始错误；学习合同没有成员标识时保持空，不编造关联。</summary>
    /// <param name="WordGroupId">错误所属整组。</param>
    /// <param name="MemberId">复习错误所属成员；原学习合同没有此字段。</param>
    /// <param name="FormCode">错误对应的词形代码。</param>
    /// <param name="CorrectAnswer">对应词形的正确答案。</param>
    public sealed record PracticeErrorSummary(string WordGroupId, long? MemberId, string FormCode, string CorrectAnswer);
}
