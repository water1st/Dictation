namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示准备持久化的完整词组及其成员快照。
    /// </summary>
    /// <param name="Language">词组语言的稳定名称。</param>
    /// <param name="AnchorSourceSenseId">词组锚定义项标识。</param>
    /// <param name="Members">按词库权威顺序保存的全部成员。</param>
    public sealed record ImportWordGroupSnapshotPo(
        string Language,
        string AnchorSourceSenseId,
        IReadOnlyList<ImportWordGroupMemberPo> Members);
}
