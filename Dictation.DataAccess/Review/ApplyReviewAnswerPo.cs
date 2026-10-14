using Dictation.DataAccess.Practice;

namespace Dictation.DataAccess.Review
{
    /// <summary>表示须在同一事务中保存的复习作答、目标成绩、记忆与下一题。</summary>
    public sealed record ApplyReviewAnswerPo(string SessionId, long QuestionId, string SubmittedText, bool WasCorrect,
        IReadOnlyList<ReviewFormOutcomePo> Outcomes, DateTimeOffset AnsweredAtUtc, string AnsweredLocalDate,
        CreatePracticeQuestionPo? NextQuestion, IReadOnlyList<string> CompletedWordGroupIds,
        bool CompleteSession, IReadOnlyList<ReviewFormMemoryPo> Memories);
}
