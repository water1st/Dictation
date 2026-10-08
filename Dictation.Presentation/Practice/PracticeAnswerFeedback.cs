using Dictation.Core.Practice;

namespace Dictation.Presentation.Practice
{
    /// <summary>保留两个业务模式返回的客观正误、正确答案和下一步，供同一页面绑定。</summary>
    public sealed record PracticeAnswerFeedback(bool WasCorrect, string CorrectAnswer, bool HadErrorInSession,
        PracticeQuestionDto? NextQuestion, PracticeSessionSummary? Summary, string SpokenText);
}
