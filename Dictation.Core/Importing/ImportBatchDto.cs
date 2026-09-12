using Dictation.Core.Configuration;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 表示一个导入批次及其全部项目的业务快照。
    /// </summary>
    public sealed class ImportBatchDto
    {
        /// <summary>
        /// 初始化导入批次业务快照。
        /// </summary>
        /// <param name="batchId">批次稳定标识。</param>
        /// <param name="language">批次对应的学习语言。</param>
        /// <param name="sourceKind">批次来源渠道。</param>
        /// <param name="status">批次当前处理状态。</param>
        /// <param name="createdAtUtc">批次创建时间。</param>
        /// <param name="completedAtUtc">批次完成时间；尚未完成时为 <see langword="null"/>。</param>
        /// <param name="items">批次中的全部项目。</param>
        public ImportBatchDto(
            string batchId,
            LearningLanguage language,
            ImportSourceKind sourceKind,
            ImportBatchStatus status,
            DateTimeOffset createdAtUtc,
            DateTimeOffset? completedAtUtc,
            IReadOnlyList<ImportBatchItemDto> items)
        {
            BatchId = batchId;
            Language = language;
            SourceKind = sourceKind;
            Status = status;
            CreatedAtUtc = createdAtUtc;
            CompletedAtUtc = completedAtUtc;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public string BatchId { get; }

        public LearningLanguage Language { get; }

        public ImportSourceKind SourceKind { get; }

        public ImportBatchStatus Status { get; }

        public DateTimeOffset CreatedAtUtc { get; }

        public DateTimeOffset? CompletedAtUtc { get; }

        public IReadOnlyList<ImportBatchItemDto> Items { get; }
    }
}
