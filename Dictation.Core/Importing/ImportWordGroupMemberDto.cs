using Dictation.Core.Lexicon;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 表示词组成员及其中文释义、词形和来源元数据。
    /// </summary>
    public sealed class ImportWordGroupMemberDto
    {
        /// <summary>
        /// 初始化词组成员业务快照。
        /// </summary>
        /// <param name="language">成员语言的稳定名称。</param>
        /// <param name="sourceSenseId">来源义项稳定标识。</param>
        /// <param name="sourceLexemeId">来源词位稳定标识。</param>
        /// <param name="headword">词典主词形。</param>
        /// <param name="reading">可选读音。</param>
        /// <param name="partOfSpeechLabel">面向用户的词性标签。</param>
        /// <param name="attributeLabel">面向用户的属性标签。</param>
        /// <param name="isPhrase">是否为短语。</param>
        /// <param name="meanings">可作为中文输出的释义。</param>
        /// <param name="forms">成员的完整词形集合。</param>
        /// <param name="meaningSource">中文释义来源。</param>
        /// <param name="meaningPrecision">中文释义精度。</param>
        /// <param name="kanjiUsageCode">日语汉字使用类别；不适用时为 <see langword="null"/>。</param>
        public ImportWordGroupMemberDto(
            string language,
            string sourceSenseId,
            string sourceLexemeId,
            string headword,
            string? reading,
            string partOfSpeechLabel,
            string attributeLabel,
            bool isPhrase,
            IReadOnlyList<string> meanings,
            IReadOnlyList<ImportWordGroupFormDto> forms,
            LexiconMeaningSource meaningSource = LexiconMeaningSource.Missing,
            LexiconMeaningPrecision meaningPrecision = LexiconMeaningPrecision.Missing,
            string? kanjiUsageCode = null)
        {
            Language = language;
            SourceSenseId = sourceSenseId;
            SourceLexemeId = sourceLexemeId;
            Headword = headword;
            Reading = reading;
            PartOfSpeechLabel = partOfSpeechLabel;
            AttributeLabel = attributeLabel;
            IsPhrase = isPhrase;
            Meanings = meanings ?? throw new ArgumentNullException(nameof(meanings));
            Forms = forms ?? throw new ArgumentNullException(nameof(forms));
            MeaningSource = meaningSource;
            MeaningPrecision = meaningPrecision;
            KanjiUsageCode = kanjiUsageCode;
        }

        public string Language { get; }

        public string SourceSenseId { get; }

        public string SourceLexemeId { get; }

        public string Headword { get; }

        public string? Reading { get; }

        public string PartOfSpeechLabel { get; }

        public string AttributeLabel { get; }

        public bool IsPhrase { get; }

        public IReadOnlyList<string> Meanings { get; }

        public IReadOnlyList<ImportWordGroupFormDto> Forms { get; }

        public LexiconMeaningSource MeaningSource { get; }

        public LexiconMeaningPrecision MeaningPrecision { get; }

        public string? KanjiUsageCode { get; }

        /// <summary>
        /// 获取成员是否至少包含一条非空中文释义。
        /// </summary>
        public bool HasChineseMeaning => Meanings.Any(meaning => !string.IsNullOrWhiteSpace(meaning));
    }
}
