namespace Dictation.DataAccess.Learning
{
    /// <summary>携带共享学习观察要原子更新的独立目标身份与业务计算结果。</summary>
    /// <param name="WordGroupId">目标所属整组。</param>
    /// <param name="MemberId">实际词组成员。</param>
    /// <param name="FormCode">本次观察的词形代码。</param>
    /// <param name="CorrectStreak">更新后的连续正确次数。</param>
    /// <param name="IsMastered">该形式是否已满足掌握条件。</param>
    /// <param name="NextEligibleOrdinal">下一次允许练习的题目序号。</param>
    public sealed record LearningFormOutcomePo(string WordGroupId, long MemberId, string FormCode,
        int CorrectStreak, bool IsMastered, int NextEligibleOrdinal);
}
