namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示从数据库读取的完整词组及其成员。
    /// </summary>
    /// <param name="WordGroupId">词组稳定标识。</param>
    /// <param name="Language">词组语言的稳定名称。</param>
    /// <param name="AnchorSourceSenseId">词组锚定义项标识。</param>
    /// <param name="Placement">词组所属业务区域的稳定名称。</param>
    /// <param name="CreatedAtUtc">词组创建时间。</param>
    /// <param name="Members">按持久化顺序返回的全部成员。</param>
    public sealed record ImportWordGroupPo(
        string WordGroupId,
        string Language,
        string AnchorSourceSenseId,
        string Placement,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<ImportWordGroupMemberPo> Members);
}
