namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示从数据库读取的导入批次及全部项目。
    /// </summary>
    /// <param name="BatchId">批次稳定标识。</param>
    /// <param name="Language">学习语言的稳定名称。</param>
    /// <param name="SourceKind">导入来源的稳定名称。</param>
    /// <param name="Status">批次状态的稳定名称。</param>
    /// <param name="CreatedAtUtc">批次创建时间。</param>
    /// <param name="CompletedAtUtc">批次完成时间；尚未完成时为 <see langword="null"/>。</param>
    /// <param name="Items">批次中的全部项目。</param>
    public sealed record ImportBatchPo(
        string BatchId,
        string Language,
        string SourceKind,
        string Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        IReadOnlyList<ImportBatchItemPo> Items);
}
