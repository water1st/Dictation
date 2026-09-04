using Dictation.Services.Configuration;

namespace Dictation.Services.Lexicon
{
    public enum LexiconMeaningSource
    {
        Tomoshi,
        ChineseOpenWordNet,
        Ecdict,
        User,
        Missing
    }

    public enum LexiconMeaningPrecision
    {
        Exact,
        Broad,
        UserProvided,
        Missing
    }

    public sealed class LexiconLookupResultDto
    {
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
            LexiconMeaningPrecision meaningPrecision = LexiconMeaningPrecision.Missing)
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

        public bool HasChineseMeaning => Meanings.Any(meaning => !string.IsNullOrWhiteSpace(meaning));

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("查词结果字段不能为空。", parameterName)
                : value;
        }
    }

    public sealed class LexiconLookupFormDto
    {
        public LexiconLookupFormDto(
            string surface,
            string? reading,
            string formLabel,
            string detailLabel,
            bool isCommon,
            bool isPrimary)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("词形不能为空。", nameof(surface))
                : surface;
            Reading = reading;
            FormLabel = string.IsNullOrWhiteSpace(formLabel)
                ? throw new ArgumentException("词形标签不能为空。", nameof(formLabel))
                : formLabel;
            DetailLabel = string.IsNullOrWhiteSpace(detailLabel)
                ? throw new ArgumentException("词形详情标签不能为空。", nameof(detailLabel))
                : detailLabel;
            IsCommon = isCommon;
            IsPrimary = isPrimary;
        }

        public string Surface { get; }

        public string? Reading { get; }

        public string FormLabel { get; }

        public string DetailLabel { get; }

        public bool IsCommon { get; }

        public bool IsPrimary { get; }
    }
}
