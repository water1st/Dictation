namespace Dictation.Core.Importing
{
    /// <summary>
    /// 提供确认导入项目和维护待处理中文释义的能力。
    /// </summary>
    public interface IImportInboxActions
    {
        /// <summary>
        /// 对批次中的指定项目执行导入决定。
        /// </summary>
        /// <param name="request">批次编号、项目编号及目标决定。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        Task DecideAsync(
            DecideImportBatchRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新导入词组中指定来源义项的中文释义。
        /// </summary>
        /// <param name="request">词组、来源义项及替换后的中文释义。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        Task UpdateMeaningsAsync(
            UpdateImportMeaningsRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
