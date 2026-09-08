namespace Dictation.DataAccess.Lexicon
{
    /// <summary>
    /// 表示从日语词库读取的一条完整查询记录。
    /// </summary>
    public sealed class JapaneseLexiconLookupPo
    {
        /// <summary>
        /// 创建包含分类、中文释义元数据和完整词形的日语记录。
        /// </summary>
        /// <param name="entryId">词条稳定标识。</param>
        /// <param name="primaryWritten">主书写形式。</param>
        /// <param name="primaryReading">主读音。</param>
        /// <param name="lexicalCategory">词汇分类代码。</param>
        /// <param name="verbClass">动词分类代码。</param>
        /// <param name="transitivity">及物性代码。</param>
        /// <param name="kanjiUsage">汉字使用代码。</param>
        /// <param name="isPhrase">是否为短语。</param>
        /// <param name="partsOfSpeech">来源词性列表。</param>
        /// <param name="meaningSource">中文释义来源代码。</param>
        /// <param name="meaningPrecision">中文释义精度代码。</param>
        /// <param name="meanings">中文释义。</param>
        /// <param name="forms">完整词形。</param>
        public JapaneseLexiconLookupPo(
            string entryId,
            string primaryWritten,
            string primaryReading,
            string lexicalCategory,
            string verbClass,
            string transitivity,
            string kanjiUsage,
            bool isPhrase,
            IReadOnlyList<string> partsOfSpeech,
            string meaningSource,
            string meaningPrecision,
            IReadOnlyList<string> meanings,
            IReadOnlyList<JapaneseLexiconLookupFormPo> forms)
        {
            EntryId = RequireText(entryId, nameof(entryId));
            PrimaryWritten = RequireText(primaryWritten, nameof(primaryWritten));
            PrimaryReading = RequireText(primaryReading, nameof(primaryReading));
            LexicalCategory = RequireText(lexicalCategory, nameof(lexicalCategory));
            VerbClass = RequireText(verbClass, nameof(verbClass));
            Transitivity = transitivity ?? throw new ArgumentNullException(nameof(transitivity));
            KanjiUsage = RequireText(kanjiUsage, nameof(kanjiUsage));
            IsPhrase = isPhrase;
            PartsOfSpeech = partsOfSpeech ?? throw new ArgumentNullException(nameof(partsOfSpeech));
            MeaningSource = RequireText(meaningSource, nameof(meaningSource));
            MeaningPrecision = RequireText(meaningPrecision, nameof(meaningPrecision));
            Meanings = meanings ?? throw new ArgumentNullException(nameof(meanings));
            Forms = forms ?? throw new ArgumentNullException(nameof(forms));
        }

        public string EntryId { get; }

        public string PrimaryWritten { get; }

        public string PrimaryReading { get; }

        public string LexicalCategory { get; }

        public string VerbClass { get; }

        public string Transitivity { get; }

        public string KanjiUsage { get; }

        public bool IsPhrase { get; }

        public IReadOnlyList<string> PartsOfSpeech { get; }

        public string MeaningSource { get; }

        public string MeaningPrecision { get; }

        public IReadOnlyList<string> Meanings { get; }

        public IReadOnlyList<JapaneseLexiconLookupFormPo> Forms { get; }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("词库字段不能为空。", parameterName)
                : value;
        }
    }
}
