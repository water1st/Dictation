using Dictation.Core.Configuration;
using Dictation.DataAccess.Importing;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 将导入收件箱持久化批次转换为业务批次和暂存统计结果。
    /// </summary>
    internal static class ImportInboxMapper
    {
        /// <summary>
        /// 将已持久化批次转换为包含派生统计的暂存结果。
        /// </summary>
        /// <param name="batch">数据访问层返回的完整批次。</param>
        /// <returns>包含业务批次以及新增、重复数量的暂存结果。</returns>
        internal static StageImportBatchResultDto ToStageResultDto(ImportBatchPo batch)
        {
            return new StageImportBatchResultDto(ToDto(batch));
        }

        /// <summary>
        /// 将持久化批次及其项目转换为业务快照。
        /// </summary>
        /// <param name="batch">数据访问层返回的完整批次。</param>
        /// <returns>保留批次状态、项目决定与完整词组的业务对象。</returns>
        internal static ImportBatchDto ToDto(ImportBatchPo batch)
        {
            return new ImportBatchDto(
                batch.BatchId,
                Enum.Parse<LearningLanguage>(batch.Language, ignoreCase: false),
                Enum.Parse<ImportSourceKind>(batch.SourceKind, ignoreCase: false),
                Enum.Parse<ImportBatchStatus>(batch.Status, ignoreCase: false),
                batch.CreatedAtUtc,
                batch.CompletedAtUtc,
                batch.Items.Select(ToDto).ToArray());
        }

        /// <summary>
        /// 将批次条目投影为业务 DTO；存在词组快照时复用共享词组映射。
        /// </summary>
        private static ImportBatchItemDto ToDto(ImportBatchItemPo item)
        {
            return new ImportBatchItemDto(
                item.ItemId,
                item.SourceText,
                item.AnchorSourceSenseId,
                item.WordGroupId,
                Enum.Parse<ImportItemDecision>(item.Decision, ignoreCase: false),
                item.WordGroup is null ? null : ImportWordGroupMapper.ToDto(item.WordGroup));
        }
    }
}
