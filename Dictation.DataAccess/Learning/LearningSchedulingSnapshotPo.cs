namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示一次出题决策需要的持久化会话、待答题、词形与历史快照。
    /// </summary>
    public sealed record LearningSchedulingSnapshotPo(
        LearningSessionPo Session,
        LearningQuestionStatePo? PendingQuestion,
        IReadOnlyList<LearningFormUnitPo> Units,
        IReadOnlyList<LearningQuestionHistoryPo> History);
}
