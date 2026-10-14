namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示学习库查询的持久化分页结果。
    /// </summary>
    public sealed record LearningLibraryPagePo(
        int TotalCount,
        IReadOnlyList<LearningWordGroupPo> Groups);
}
