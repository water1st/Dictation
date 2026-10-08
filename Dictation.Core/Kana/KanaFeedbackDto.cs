namespace Dictation.Core.Kana
{
    /// <summary>返回客观反馈、下一题和原错误结算，不包含主观记忆标签。</summary>
    /// <param name="IsCorrect">本次是否正确。</param>
    /// <param name="CorrectAnswer">作答后展示的正确答案。</param>
    /// <param name="NextQuestion">下一题，轮次结束时为空。</param>
    /// <param name="IsCompleted">本轮是否全部完成。</param>
    /// <param name="CompletedCount">已完成项目数。</param>
    /// <param name="TotalCount">本轮项目数。</param>
    /// <param name="Errors">所有原始错误观察。</param>
    public sealed record KanaFeedbackDto(bool IsCorrect, string CorrectAnswer, KanaQuestionDto? NextQuestion,
        bool IsCompleted, int CompletedCount, int TotalCount, IReadOnlyList<KanaErrorDto> Errors);
}
