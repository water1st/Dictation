namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示原子应用答题结果、词形状态与下一题的持久化输入。
    /// </summary>
    public sealed record ApplyLearningAnswerPo(
        string SessionId,
        long QuestionId,
        string SubmittedText,
        bool WasCorrect,
        int CorrectStreak,
        bool IsMastered,
        int NextEligibleOrdinal,
        DateTimeOffset AnsweredAtUtc,
        string AnsweredLocalDate,
        CreateLearningQuestionPo? NextQuestion,
        IReadOnlyList<string> CompletedWordGroupIds,
        bool CompleteSession);
}
