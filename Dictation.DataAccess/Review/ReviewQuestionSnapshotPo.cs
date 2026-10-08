using Dictation.DataAccess.Practice;

namespace Dictation.DataAccess.Review
{
    /// <summary>表示同一读事务内的独立复习计划、状态、题目与历史，不读取学习成绩。</summary>
    public sealed record ReviewQuestionSnapshotPo(
        ReviewSessionPo Session,
        string Status,
        int CurrentOrdinal,
        PracticeQuestionStatePo? PendingQuestion,
        IReadOnlyList<PracticeFormUnitPo> Units,
        IReadOnlyList<PracticeQuestionHistoryPo> History,
        IReadOnlyList<ReviewStoredMemoryPo> Memories);
}
