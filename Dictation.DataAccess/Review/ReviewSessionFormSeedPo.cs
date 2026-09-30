namespace Dictation.DataAccess.Review
{
    /// <summary>以稳定义项与词形定位成员，不把假名／汉字分成两份记忆。</summary>
    /// <param name="SourceSenseId">属于该组的成员来源义项标识。</param>
    /// <param name="FormCode">成员词形稳定代码。</param>
    public sealed record ReviewSessionFormSeedPo(string SourceSenseId, string FormCode);
}
