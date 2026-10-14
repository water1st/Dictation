namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示创建会话时查询到的候选词组及已有词形进度。
    /// </summary>
    public sealed record LearningSessionCandidatePo(
        string WordGroupId,
        DateTimeOffset CreatedAtUtc,
        bool HasProgress,
        IReadOnlyList<LearningFormIdentityPo> AvailableForms,
        IReadOnlyList<LearningFormStateSummaryPo> FormStates);
}
