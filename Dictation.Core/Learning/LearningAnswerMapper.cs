using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Practice;

namespace Dictation.Core.Learning
{
    /// <summary>纯转换学习词形状态为共享目标的持久化结果，不执行业务或数据库操作。</summary>
    internal static class LearningAnswerMapper
    {
        /// <summary>提取已由业务层计算的身份、掌握状态与重练序号。</summary>
        /// <param name="unit">应用客观答题后得到的词形状态。</param>
        /// <returns>不包含展示属性的最小写入结果。</returns>
        internal static LearningFormOutcomePo ToOutcome(PracticeFormUnitPo unit) =>
            new(unit.WordGroupId, unit.MemberId, unit.FormCode, unit.CorrectStreak, unit.IsMastered, unit.NextEligibleOrdinal);
    }
}
