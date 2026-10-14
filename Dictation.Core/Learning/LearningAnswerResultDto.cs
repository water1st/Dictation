using Dictation.Core.Practice;
namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示一次答案提交后的反馈和后续会话状态。
    /// </summary>
    public sealed record LearningAnswerResultDto(
        bool WasCorrect,
        string CorrectAnswer,
        bool HadErrorInSession,
        PracticeQuestionDto? NextQuestion,
        LearningSessionSummaryDto? Summary,
        string SpokenText);
}
