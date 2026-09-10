namespace Dictation.Core.Importing
{
    /// <summary>
    /// 提供将外部选择暂存为完整导入批次的能力。
    /// </summary>
    public interface IImportStaging
    {
        /// <summary>
        /// 规范化并去重用户选择，将每个来源义项的完整词组快照暂存到导入收件箱。
        /// </summary>
        /// <param name="request">包含学习语言、导入来源及所选义项的暂存请求。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>持久化后的批次，以及新增和重复项目统计。</returns>
        Task<StageImportBatchResultDto> StageAsync(
            StageImportBatchRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
