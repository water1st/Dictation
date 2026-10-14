namespace Dictation.Core.Importing
{
    /// <summary>
    /// 描述对同一批次内一组待处理项目执行决定的请求。
    /// </summary>
    /// <param name="BatchId">待处理批次的稳定标识。</param>
    /// <param name="ItemIds">批次内需要处理的项目编号。</param>
    /// <param name="Decision">统一应用到所选项目的决定。</param>
    public sealed record DecideImportBatchRequestDto(
        string BatchId,
        IReadOnlyList<long> ItemIds,
        ImportDecision Decision);
}
