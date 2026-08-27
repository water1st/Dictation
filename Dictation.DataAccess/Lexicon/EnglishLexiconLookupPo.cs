namespace Dictation.DataAccess.Lexicon
{
    public sealed class EnglishLexiconLookupPo
    {
        public EnglishLexiconLookupPo(
            string senseId,
            string lexemeId,
            string lemma,
            string sourceEntryKey,
            string partOfSpeech,
            bool isPhrase,
            IReadOnlyList<string> meanings,
            IReadOnlyList<EnglishLexiconLookupFormPo> forms)
        {
            SenseId = RequireText(senseId, nameof(senseId));
            LexemeId = RequireText(lexemeId, nameof(lexemeId));
            Lemma = RequireText(lemma, nameof(lemma));
            SourceEntryKey = RequireText(sourceEntryKey, nameof(sourceEntryKey));
            PartOfSpeech = RequireText(partOfSpeech, nameof(partOfSpeech));
            IsPhrase = isPhrase;
            Meanings = meanings ?? throw new ArgumentNullException(nameof(meanings));
            Forms = forms ?? throw new ArgumentNullException(nameof(forms));
        }

        public string SenseId { get; }

        public string LexemeId { get; }

        public string Lemma { get; }

        public string SourceEntryKey { get; }

        public string PartOfSpeech { get; }

        public bool IsPhrase { get; }

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
