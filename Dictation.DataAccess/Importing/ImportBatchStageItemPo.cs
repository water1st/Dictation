namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示导入批次中一个待暂存的来源选择及完整词组快照。
    /// </summary>
    /// <param name="SourceText">触发导入的原始词形。</param>
    /// <param name="AnchorSourceSenseId">词组锚定义项标识。</param>
    /// <param name="Group">准备持久化的完整词组。</param>
    public sealed record ImportBatchStageItemPo(
        string SourceText,
        string AnchorSourceSenseId,
        ImportWordGroupSnapshotPo Group);
}
