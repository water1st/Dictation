namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示写入数据访问层的完整导入批次。
    /// </summary>
    /// <param name="Language">学习语言的稳定名称。</param>
    /// <param name="SourceKind">导入来源的稳定名称。</param>
    /// <param name="Items">需要在同一事务中暂存的项目。</param>
    public sealed record ImportBatchStagePo(
        string Language,
        string SourceKind,
        IReadOnlyList<ImportBatchStageItemPo> Items);
}
