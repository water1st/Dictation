namespace Dictation.Core.Mistakes
{
    /// <summary>按独立义项及词形归组的累计错题，不复制学习词条或创建新的记忆。</summary>
    /// <param name="WordGroupId">当前用户整组标识。</param>
    /// <param name="SourceSenseId">组内独立义项标识。</param>
    /// <param name="FormCode">发生错误的词形码。</param>
    /// <param name="Headword">所属成员词头。</param>
    /// <param name="FormLabel">中文词形说明。</param>
    /// <param name="Meanings">用户快照中文释义。</param>
    /// <param name="Placement">当前所在Learning或Review库。</param>
    /// <param name="DueAtUtc">原FSRS到期时间，未建立记忆时为空。</param>
    /// <param name="Attempts">按最近时间排列的历史错误，正确重试不删除原始审计。</param>
    public sealed record WrongAnswerItemDto(string WordGroupId, string SourceSenseId, string FormCode,
        string Headword, string FormLabel, string Meanings, string Placement, DateTimeOffset? DueAtUtc,
        IReadOnlyList<WrongAnswerAttemptDto> Attempts);
}
