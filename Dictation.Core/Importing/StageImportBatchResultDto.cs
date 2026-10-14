namespace Dictation.Core.Importing
{
    /// <summary>
    /// 表示暂存后的批次及由批次项目状态派生的新增、重复统计。
    /// </summary>
    public sealed class StageImportBatchResultDto
    {
        /// <summary>
        /// 初始化暂存结果。
        /// </summary>
        /// <param name="batch">持久化后返回的完整批次。</param>
        public StageImportBatchResultDto(ImportBatchDto batch)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
        }

        public ImportBatchDto Batch { get; }

        /// <summary>
        /// 获取本次真正暂存并等待确认的项目数。
        /// </summary>
        public int AddedCount => Batch.Items.Count(item => item.Decision == ImportItemDecision.Pending);

        /// <summary>
        /// 获取因已有词组而未重复暂存的项目数。
        /// </summary>
        public int DuplicateCount => Batch.Items.Count(item => item.Decision == ImportItemDecision.Duplicate);
    }
}
