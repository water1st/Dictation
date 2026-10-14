namespace Dictation.Core.Statistics
{
    /// <summary>表示累计错误较多的整组，不因成员或词形不同创建新词。</summary>
    /// <param name="WordGroupId">现存词组 ID。</param>
    /// <param name="Headword">代表错误成员的词头。</param>
    /// <param name="Meanings">对应中文释义。</param>
    /// <param name="ErrorCount">原错误观察的累计数量。</param>
    public sealed record FrequentMistakeDto(string WordGroupId, string Headword, string Meanings, long ErrorCount);
}
