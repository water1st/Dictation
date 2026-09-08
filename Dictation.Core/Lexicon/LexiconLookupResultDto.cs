using Dictation.Core.Configuration;

namespace Dictation.Core.Lexicon
{
    /// <summary>
    /// 表示可供业务与界面使用的一条词库查询结果。
    /// </summary>
    public sealed class LexiconLookupResultDto
    {
        /// <summary>
        /// 创建保留稳定来源标识、中文释义元数据和完整词形的查询结果。
        /// </summary>
        /// <param name="language">学习语言。</param>
        /// <param name="sourceSenseId">来源义项稳定标识。</param>
        /// <param name="sourceLexemeId">来源词元稳定标识。</param>
        /// <param name="headword">主词头。</param>
        /// <param name="reading">读音；英语结果为空。</param>
        /// <param name="partOfSpeechLabel">词性展示标签。</param>
        /// <param name="attributeLabel">属性展示标签。</param>
        /// <param name="isPhrase">是否为短语。</param>
        /// <param name="meanings">中文释义。</param>
        /// <param name="forms">完整词形。</param>
        /// <param name="meaningSource">中文释义来源。</param>
        /// <param name="meaningPrecision">中文释义精度。</param>
        /// <param name="kanjiUsageCode">日语汉字使用稳定代码。</param>
        public LexiconLookupResultDto(
            LearningLanguage language,
            string sourceSenseId,
            string sourceLexemeId,
            string headword,
            string? reading,
            string partOfSpeechLabel,
            string attributeLabel,
            bool isPhrase,
            IReadOnlyList<string> meanings,
            IReadOnlyList<LexiconLookupFormDto> forms,
            LexiconMeaningSource meaningSource = LexiconMeaningSource.Missing,
            LexiconMeaningPrecision meaningPrecision = LexiconMeaningPrecision.Missing,
            string? kanjiUsageCode = null)
        {
            Language = language;
            SourceSenseId = RequireText(sourceSenseId, nameof(sourceSenseId));
            SourceLexemeId = RequireText(sourceLexemeId, nameof(sourceLexemeId));
            Headword = RequireText(headword, nameof(headword));
            Reading = reading;
            PartOfSpeechLabel = RequireText(partOfSpeechLabel, nameof(partOfSpeechLabel));
            AttributeLabel = RequireText(attributeLabel, nameof(attributeLabel));
            IsPhrase = isPhrase;
            Meanings = meanings ?? throw new ArgumentNullException(nameof(meanings));
            Forms = forms ?? throw new ArgumentNullException(nameof(forms));
            MeaningSource = meaningSource;
            MeaningPrecision = meaningPrecision;
            KanjiUsageCode = kanjiUsageCode;
        }

        public LearningLanguage Language { get; }

        public string SourceSenseId { get; }

        public string SourceLexemeId { get; }

        public string Headword { get; }

        public string? Reading { get; }

        public string PartOfSpeechLabel { get; }

        public string AttributeLabel { get; }

        public bool IsPhrase { get; }

        public IReadOnlyList<string> Meanings { get; }

        public IReadOnlyList<LexiconLookupFormDto> Forms { get; }

        public LexiconMeaningSource MeaningSource { get; }

        public LexiconMeaningPrecision MeaningPrecision { get; }

        public string? KanjiUsageCode { get; }

        /// <summary>
        /// 获取结果是否包含至少一条非空中文释义。
        /// </summary>
        public bool HasChineseMeaning => Meanings.Any(meaning => !string.IsNullOrWhiteSpace(meaning));

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("查词结果字段不能为空。", parameterName)
                : value;
        }
    }
}
