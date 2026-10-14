namespace Dictation.Core.Importing
{
    /// <summary>
    /// 表示导入批次中的单个选择及其可选词组快照。
    /// </summary>
    public sealed class ImportBatchItemDto
    {
        /// <summary>
        /// 初始化导入批次项目。
        /// </summary>
        /// <param name="itemId">批次项目编号。</param>
        /// <param name="sourceText">触发导入的原始词形。</param>
        /// <param name="anchorSourceSenseId">词组锚定义项标识。</param>
        /// <param name="wordGroupId">已暂存词组标识；重复项目没有独立词组时为 <see langword="null"/>。</param>
        /// <param name="decision">项目当前暂存或处理结果。</param>
        /// <param name="wordGroup">完整词组快照；重复项目未新增词组时为 <see langword="null"/>。</param>
        public ImportBatchItemDto(
            long itemId,
            string sourceText,
            string anchorSourceSenseId,
            string? wordGroupId,
            ImportItemDecision decision,
            ImportWordGroupDto? wordGroup)
        {
            ItemId = itemId;
            SourceText = sourceText;
            AnchorSourceSenseId = anchorSourceSenseId;
            WordGroupId = wordGroupId;
            Decision = decision;
            WordGroup = wordGroup;
        }

        public long ItemId { get; }

        public string SourceText { get; }

        public string AnchorSourceSenseId { get; }

        public string? WordGroupId { get; }

        public ImportItemDecision Decision { get; }

        public ImportWordGroupDto? WordGroup { get; }
    }
}
