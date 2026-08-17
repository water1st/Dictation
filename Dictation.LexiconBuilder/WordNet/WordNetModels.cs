using System;
using System.Collections.Generic;

namespace Dictation.LexiconBuilder.WordNet
{
    internal enum EnglishPartOfSpeech
    {
        Unknown = 0,
        Noun = 1,
        Verb = 2,
        Adjective = 3,
        AdjectiveSatellite = 4,
        Adverb = 5
    }

    internal enum EnglishFormKind
    {
        Base = 0,
        PresentFirstPersonSingular = 1,
        PresentNonThirdPerson = 2,
        ThirdPersonSingular = 3,
        Past = 4,
        PastParticiple = 5,
        PresentParticiple = 6,
        Plural = 7,
        Comparative = 8,
        Superlative = 9
    }

    internal enum EnglishFormSourceKind
    {
        Source = 0,
        Rule = 1
    }

    internal sealed class EnglishForm
    {
        public EnglishForm(string surface, EnglishFormKind kind, EnglishFormSourceKind sourceKind)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("英语词形不能为空。", nameof(surface))
                : surface;
            Kind = kind;
            SourceKind = sourceKind;
        }

        public string Surface { get; }

        public EnglishFormKind Kind { get; }

        public EnglishFormSourceKind SourceKind { get; }
    }

    internal sealed class EnglishSense
    {
        public EnglishSense(
            string senseId,
            string synsetId,
            int order,
            IReadOnlyList<string> derivationSenseIds)
        {
            SenseId = RequireText(senseId, nameof(senseId));
            SynsetId = RequireText(synsetId, nameof(synsetId));
            if (order <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            Order = order;
            DerivationSenseIds = derivationSenseIds ?? throw new ArgumentNullException(nameof(derivationSenseIds));
        }

        public string SenseId { get; }

        public string SynsetId { get; }

        public int Order { get; }

        public IReadOnlyList<string> DerivationSenseIds { get; }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("WordNet 文本不能为空。", parameterName)
                : value;
        }
    }

    internal sealed class EnglishLexeme
    {
        public EnglishLexeme(
            string lexemeId,
            string lemma,
            string sourceEntryKey,
            EnglishPartOfSpeech partOfSpeech,
            IReadOnlyList<string> sourceForms,
            IReadOnlyList<EnglishSense> senses,
            bool isPhrase)
        {
            LexemeId = RequireText(lexemeId, nameof(lexemeId));
            Lemma = RequireText(lemma, nameof(lemma));
            SourceEntryKey = RequireText(sourceEntryKey, nameof(sourceEntryKey));
            SourceForms = sourceForms ?? throw new ArgumentNullException(nameof(sourceForms));
            Senses = senses ?? throw new ArgumentNullException(nameof(senses));
            PartOfSpeech = partOfSpeech;
            IsPhrase = isPhrase;
        }

        public string LexemeId { get; }

        public string Lemma { get; }

        public string SourceEntryKey { get; }

        public EnglishPartOfSpeech PartOfSpeech { get; }

        public IReadOnlyList<string> SourceForms { get; }

        public IReadOnlyList<EnglishSense> Senses { get; }

        public bool IsPhrase { get; }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("WordNet 文本不能为空。", parameterName)
                : value;
        }
    }

    internal sealed class EnglishSynset
    {
        public EnglishSynset(
            string synsetId,
            EnglishPartOfSpeech partOfSpeech,
            IReadOnlyList<string> definitions,
            IReadOnlyList<string> examples)
        {
            SynsetId = string.IsNullOrWhiteSpace(synsetId)
                ? throw new ArgumentException("synset ID 不能为空。", nameof(synsetId))
                : synsetId;
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            Examples = examples ?? throw new ArgumentNullException(nameof(examples));
            PartOfSpeech = partOfSpeech;
        }

        public string SynsetId { get; }

        public EnglishPartOfSpeech PartOfSpeech { get; }

        public IReadOnlyList<string> Definitions { get; }

        public IReadOnlyList<string> Examples { get; }
    }

    internal sealed class WordNetLexicon
    {
        public WordNetLexicon(
            IReadOnlyList<EnglishLexeme> lexemes,
            IReadOnlyList<EnglishSynset> synsets)
        {
            Lexemes = lexemes ?? throw new ArgumentNullException(nameof(lexemes));
            Synsets = synsets ?? throw new ArgumentNullException(nameof(synsets));
        }

        public IReadOnlyList<EnglishLexeme> Lexemes { get; }

        public IReadOnlyList<EnglishSynset> Synsets { get; }
    }
}
