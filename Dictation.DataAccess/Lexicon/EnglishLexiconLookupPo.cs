namespace Dictation.DataAccess.Lexicon
{
    /// <summary>
    /// 表示从英语词库读取的一条完整查询记录。
    /// </summary>
    public sealed class EnglishLexiconLookupPo
    {
        /// <summary>
        /// 创建包含中文释义元数据和完整词形的英语记录。
        /// </summary>
        /// <param name="learningSenseId">学习义项稳定标识。</param>
        /// <param name="sourceLexemeId">来源词元稳定标识。</param>
        /// <param name="headword">主词头。</param>
        /// <param name="partOfSpeech">来源词性代码。</param>
        /// <param name="isPhrase">是否为短语。</param>
        /// <param name="meaningSource">中文释义来源代码。</param>
        /// <param name="meaningPrecision">中文释义精度代码。</param>
        /// <param name="meanings">中文释义。</param>
        /// <param name="forms">完整词形。</param>
        public EnglishLexiconLookupPo(
            string learningSenseId,
            string sourceLexemeId,
            string headword,
            string partOfSpeech,
            bool isPhrase,
            string meaningSource,
            string meaningPrecision,
            IReadOnlyList<string> meanings,
            IReadOnlyList<EnglishLexiconLookupFormPo> forms)
        {
            LearningSenseId = RequireText(learningSenseId, nameof(learningSenseId));
            SourceLexemeId = RequireText(sourceLexemeId, nameof(sourceLexemeId));
            Headword = RequireText(headword, nameof(headword));
            PartOfSpeech = RequireText(partOfSpeech, nameof(partOfSpeech));
            IsPhrase = isPhrase;
            MeaningSource = RequireText(meaningSource, nameof(meaningSource));
            MeaningPrecision = RequireText(meaningPrecision, nameof(meaningPrecision));
            Meanings = meanings ?? throw new ArgumentNullException(nameof(meanings));
            Forms = forms ?? throw new ArgumentNullException(nameof(forms));
        }

        public string LearningSenseId { get; }

        public string SourceLexemeId { get; }

        public string Headword { get; }

        public string PartOfSpeech { get; }

        public bool IsPhrase { get; }

        public string MeaningSource { get; }

        public string MeaningPrecision { get; }

        public IReadOnlyList<string> Meanings { get; }

        public IReadOnlyList<EnglishLexiconLookupFormPo> Forms { get; }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("词库字段不能为空。", parameterName)
                : value;
        }
    }
}
