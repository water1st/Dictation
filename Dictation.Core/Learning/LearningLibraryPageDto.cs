namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示学习库的一个分页结果。
    /// </summary>
    public sealed record LearningLibraryPageDto(
        int PageNumber,
        int PageSize,
        int TotalCount,
        IReadOnlyList<LearningWordGroupDto> Groups);
}
