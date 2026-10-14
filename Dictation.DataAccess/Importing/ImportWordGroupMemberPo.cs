namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示持久化词组成员及其释义、词形和来源元数据。
    /// </summary>
    /// <param name="Language">成员语言的稳定名称。</param>
    /// <param name="SourceSenseId">来源义项稳定标识。</param>
    /// <param name="SourceLexemeId">来源词位稳定标识。</param>
    /// <param name="Headword">词典主词形。</param>
    /// <param name="Reading">可选读音。</param>
    /// <param name="PartOfSpeechLabel">面向用户的词性标签。</param>
    /// <param name="AttributeLabel">面向用户的属性标签。</param>
    /// <param name="IsPhrase">是否为短语。</param>
    /// <param name="MeaningLanguage">释义语言标识。</param>
    /// <param name="MeaningSource">释义来源的稳定名称。</param>
    /// <param name="MeaningPrecision">释义精度的稳定名称。</param>
    /// <param name="Meanings">按显示顺序保存的释义。</param>
    /// <param name="Forms">成员的完整词形集合。</param>
    /// <param name="KanjiUsageCode">日语汉字使用类别；不适用时为 <see langword="null"/>。</param>
    public sealed record ImportWordGroupMemberPo(
        string Language,
        string SourceSenseId,
        string SourceLexemeId,
        string Headword,
        string? Reading,
        string PartOfSpeechLabel,
        string AttributeLabel,
        bool IsPhrase,
        string MeaningLanguage,
        string MeaningSource,
        string MeaningPrecision,
        IReadOnlyList<string> Meanings,
        IReadOnlyList<ImportWordGroupFormPo> Forms,
        string? KanjiUsageCode = null);
}
