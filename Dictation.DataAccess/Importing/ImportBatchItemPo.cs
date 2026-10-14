namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示从数据库读取的导入批次项目及可选词组。
    /// </summary>
    /// <param name="ItemId">批次项目编号。</param>
    /// <param name="SourceText">触发导入的原始词形。</param>
    /// <param name="AnchorSourceSenseId">词组锚定义项标识。</param>
    /// <param name="WordGroupId">已暂存词组标识；重复项目没有独立词组时为 <see langword="null"/>。</param>
    /// <param name="Decision">项目决定的稳定名称。</param>
    /// <param name="WordGroup">完整词组；重复项目未新增词组时为 <see langword="null"/>。</param>
    public sealed record ImportBatchItemPo(
        long ItemId,
        string SourceText,
        string AnchorSourceSenseId,
        string? WordGroupId,
        string Decision,
        ImportWordGroupPo? WordGroup);
}
