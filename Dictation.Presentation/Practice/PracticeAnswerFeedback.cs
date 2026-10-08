using Dictation.Core.Practice;

namespace Dictation.Presentation.Practice
{
    /// <summary>保留两个业务模式返回的客观正误、正确答案和下一步，供同一页面绑定。</summary>
    /// <param name="WasCorrect">本次客观判断结果。</param>
    /// <param name="CorrectAnswer">提交后揭示的正确答案。</param>
    /// <param name="HadErrorInSession">当前词形在本轮是否曾答错。</param>
    /// <param name="NextQuestion">下一道业务题目；结束时为空。</param>
    /// <param name="Summary">本轮结束时的业务总结。</param>
    /// <param name="SpokenText">答案揭示后可播放的正确读音。</param>
    public sealed record PracticeAnswerFeedback(bool WasCorrect, string CorrectAnswer, bool HadErrorInSession,
        PracticeQuestionDto? NextQuestion, PracticeSessionSummary? Summary, string SpokenText);
}
