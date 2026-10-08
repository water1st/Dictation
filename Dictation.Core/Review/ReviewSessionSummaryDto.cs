namespace Dictation.Core.Review
{
    /// <summary>按整组计完成量、按实际作答计题数，并保留重练正确后仍须展示的本轮错误。</summary>
    public sealed record ReviewSessionSummaryDto(string SessionId, int CompletedWordGroupCount, int AnsweredCount,
        IReadOnlyList<ReviewErrorSummaryDto> Errors);
}
