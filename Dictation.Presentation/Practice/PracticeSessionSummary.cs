namespace Dictation.Presentation.Practice
{
    /// <summary>显示实际作答次数与完成整组数，不把词形数当成单词数。</summary>
    public sealed record PracticeSessionSummary(string SessionId, int AnsweredCount,
        IReadOnlyList<PracticeErrorSummary> Errors, int CompletedWordGroupCount);
}
