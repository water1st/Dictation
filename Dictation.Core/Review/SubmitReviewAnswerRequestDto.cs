namespace Dictation.Core.Review
{
    /// <summary>提交当前复习题目的客观输入；空答案按错误处理，不表示主观记忆评级。</summary>
    /// <param name="SessionId">所属复习会话。</param>
    /// <param name="QuestionId">当前持久化待答题的标识。</param>
    /// <param name="SubmittedText">用户输入或 OCR 识别的文本，允许空答案。</param>
    public sealed record SubmitReviewAnswerRequestDto(string SessionId, long QuestionId, string? SubmittedText);
}
