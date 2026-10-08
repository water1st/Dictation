using Dictation.DataAccess.Practice;
using Dictation.DataAccess.Review;

namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示原子应用答题结果、词形状态与下一题的持久化输入。
    /// </summary>
    /// <param name="SessionId">当前学习会话标识。</param>
    /// <param name="QuestionId">尚未提交的实际题目编号。</param>
    /// <param name="SubmittedText">规范化后的用户答案。</param>
    /// <param name="WasCorrect">本次客观判题结果。</param>
    /// <param name="CorrectStreak">主目标更新后的连续正确次数。</param>
    /// <param name="IsMastered">主目标是否已满足掌握条件。</param>
    /// <param name="NextEligibleOrdinal">主目标下次允许重练的序号。</param>
    /// <param name="AnsweredAtUtc">业务时钟提供的作答时间。</param>
    /// <param name="AnsweredLocalDate">按用户当地日期记录的完成日。</param>
    /// <param name="NextQuestion">共同提交的下一题，完成时为空。</param>
    /// <param name="CompletedWordGroupIds">本次完成的整组标识，不按共享观察数量计数。</param>
    /// <param name="CompleteSession">是否结束本轮学习。</param>
    /// <param name="MemorySeeds">本次完成词组的首次记忆状态，已有记忆不覆盖。</param>
    /// <param name="AdditionalOutcomes">完全相同听写所共享的其它目标成绩，不能重复主目标。</param>
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
        CreatePracticeQuestionPo? NextQuestion,
        IReadOnlyList<string> CompletedWordGroupIds,
        bool CompleteSession,
        IReadOnlyList<ReviewFormMemoryPo>? MemorySeeds = null,
        IReadOnlyList<LearningFormOutcomePo>? AdditionalOutcomes = null);
}
