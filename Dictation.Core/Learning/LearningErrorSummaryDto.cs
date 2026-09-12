namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示会话内一个曾答错词形的汇总。
    /// </summary>
    public sealed record LearningErrorSummaryDto(
        string WordGroupId,
        string FormCode,
        string CorrectAnswer);
}
