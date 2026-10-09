namespace Dictation.Core.Importing
{
    /// <summary>按标识只读回查完整导入批次，包含用户已处理的批次。</summary>
    public interface IImportBatchReader
    {
        /// <summary>读取原批次快照，不暂存词条或改变用户决定。</summary>
        /// <param name="batchId">批次稳定标识。</param>
        /// <param name="cancellationToken">取消只读操作。</param>
        /// <returns>完整批次；不存在时抛出原收件箱访问异常。</returns>
        Task<ImportBatchDto> GetBatchAsync(string batchId, CancellationToken cancellationToken = default);
    }
}
