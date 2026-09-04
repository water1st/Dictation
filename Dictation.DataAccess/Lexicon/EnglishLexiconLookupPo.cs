namespace Dictation.DataAccess.Lexicon
{
    public sealed class EnglishLexiconLookupPo
    {
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

    public sealed class EnglishLexiconLookupFormPo
    {
        public EnglishLexiconLookupFormPo(
            string surface,
            string formKind,
            string formSourceKind)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("词形不能为空。", nameof(surface))
                : surface;
            FormKind = string.IsNullOrWhiteSpace(formKind)
                ? throw new ArgumentException("词形类型不能为空。", nameof(formKind))
                : formKind;
            FormSourceKind = string.IsNullOrWhiteSpace(formSourceKind)
                ? throw new ArgumentException("词形来源类型不能为空。", nameof(formSourceKind))
                : formSourceKind;
        }

        public string Surface { get; }

        public string FormKind { get; }

        public string FormSourceKind { get; }
    }
}
