namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示对当前学习题目的答案提交。
    /// </summary>
    public sealed record SubmitLearningAnswerRequestDto(
        string SessionId,
        long QuestionId,
        string? SubmittedText);
}
