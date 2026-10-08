using Dictation.Core.Practice;

namespace Dictation.Core.Review
{
    /// <summary>表示客观反馈、正确答案读音及已经原子保存的后续题目或总结。</summary>
    public sealed record ReviewAnswerResultDto(bool WasCorrect, string ExpectedAnswer, string ExpectedSpokenText,
        bool HadErrorInSession, PracticeQuestionDto? NextQuestion, ReviewSessionSummaryDto? Summary);
}
