namespace Dictation.DataAccess.Lexicon
{
    public sealed class JapaneseLexiconLookupPo
    {
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

    public sealed class JapaneseLexiconLookupFormPo
    {
        public JapaneseLexiconLookupFormPo(
            string surface,
            string reading,
            string formKind,
            string scriptKind,
            string lexicalRole,
            bool isCommon,
            bool isPrimary)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("词形不能为空。", nameof(surface))
                : surface;
            Reading = string.IsNullOrWhiteSpace(reading)
                ? throw new ArgumentException("读音不能为空。", nameof(reading))
                : reading;
            FormKind = string.IsNullOrWhiteSpace(formKind)
                ? throw new ArgumentException("词形类型不能为空。", nameof(formKind))
                : formKind;
            ScriptKind = string.IsNullOrWhiteSpace(scriptKind)
                ? throw new ArgumentException("文字类型不能为空。", nameof(scriptKind))
                : scriptKind;
            LexicalRole = string.IsNullOrWhiteSpace(lexicalRole)
                ? throw new ArgumentException("词汇角色不能为空。", nameof(lexicalRole))
                : lexicalRole;
            IsCommon = isCommon;
            IsPrimary = isPrimary;
        }

        public string Surface { get; }

        public string Reading { get; }

        public string FormKind { get; }

        public string ScriptKind { get; }

        public string LexicalRole { get; }

        public bool IsCommon { get; }

        public bool IsPrimary { get; }
    }
}
