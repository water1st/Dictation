namespace Dictation.Presentation.Practice
{
    /// <summary>显示实际作答次数与完成整组数，不把词形数当成单词数。</summary>
    /// <param name="SessionId">已完成的业务会话标识。</param>
    /// <param name="AnsweredCount">实际提交答案的次数，包含错后重练。</param>
    /// <param name="Errors">本轮曾错的词形，不因之后答对而移除。</param>
    /// <param name="CompletedWordGroupCount">本轮完成的整组数量。</param>
    public sealed record PracticeSessionSummary(string SessionId, int AnsweredCount,
        IReadOnlyList<PracticeErrorSummary> Errors, int CompletedWordGroupCount);
}
