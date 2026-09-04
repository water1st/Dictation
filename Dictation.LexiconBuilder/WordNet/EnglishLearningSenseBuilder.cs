using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dictation.LexiconBuilder;

namespace Dictation.LexiconBuilder.WordNet
{
    internal sealed record EnglishLearningSense(
        string LearningSenseId,
        string SourceLexemeId,
        string Headword,
        EnglishPartOfSpeech PartOfSpeech,
        bool IsPhrase,
        string MeaningSource,
        string MeaningPrecision,
        int SortOrder,
        IReadOnlyList<string> SourceSenseIds,
        IReadOnlyList<string> Meanings);

    internal sealed class EnglishLearningSenseBuilder
    {
        internal IReadOnlyList<EnglishLearningSense> Build(
            WordNetLexicon lexicon,
            IReadOnlyDictionary<string, IReadOnlyList<string>> chineseMeaningsByIli,
            EcdictTranslationReader ecdict)
        {
            ArgumentNullException.ThrowIfNull(lexicon);
            ArgumentNullException.ThrowIfNull(chineseMeaningsByIli);
            ArgumentNullException.ThrowIfNull(ecdict);

            var synsetById = lexicon.Synsets.ToDictionary(synset => synset.SynsetId, StringComparer.Ordinal);
            var families = lexicon.Lexemes
                .Select((lexeme, index) => new IndexedLexeme(lexeme, index))
                .GroupBy(
                    item => new FamilyKey(NormalizeLemma(item.Lexeme.Lemma), item.Lexeme.PartOfSpeech))
                .OrderBy(group => group.Min(item => item.Index));
            var results = new List<EnglishLearningSense>();
            var sortOrder = 0;

            foreach (var family in families)
            {
                var exact = new List<EnglishLearningSense>();
                var uncovered = new List<SourceSense>();
                foreach (var item in family.OrderBy(item => item.Index))
                {
                    foreach (var sense in item.Lexeme.Senses.OrderBy(sense => sense.Order).ThenBy(sense => sense.SenseId, StringComparer.Ordinal))
                    {
                        if (!synsetById.TryGetValue(sense.SynsetId, out var synset))
                        {
                            throw new LexiconBuildException($"英语词条 {item.Lexeme.LexemeId} 引用了不存在的 synset：{sense.SynsetId}。");
                        }

                        var meanings = ReadExactMeanings(synset.Ili, chineseMeaningsByIli);
                        if (meanings.Count == 0)
                        {
                            uncovered.Add(new SourceSense(item.Lexeme, sense));
                            continue;
                        }

                        exact.Add(new EnglishLearningSense(
                            sense.SenseId,
                            item.Lexeme.LexemeId,
                            item.Lexeme.Lemma,
                            item.Lexeme.PartOfSpeech,
                            item.Lexeme.IsPhrase,
                            "ChineseOpenWordNet",
                            "Exact",
                            sortOrder++,
                            new[] { sense.SenseId },
                            meanings));
                    }
                }

                results.AddRange(exact);
                if (uncovered.Count > 0)
                {
                    results.Add(CreateFallback(
                        family.Key,
                        uncovered,
                        ecdict,
                        sortOrder++));
                }
            }

            return results;
        }

        private static EnglishLearningSense CreateFallback(
            FamilyKey family,
            IReadOnlyList<SourceSense> uncovered,
            EcdictTranslationReader ecdict,
            int sortOrder)
        {
            var meanings = ecdict.Read(family.NormalizedLemma, family.PartOfSpeech);
            var hasMeanings = meanings.Count > 0;
            var partOfSpeechCode = ToCode(family.PartOfSpeech);
            return new EnglishLearningSense(
                $"{(hasMeanings ? "ecdict" : "missing")}:{family.NormalizedLemma}:{partOfSpeechCode}",
                $"oewn:{family.NormalizedLemma}:{partOfSpeechCode}",
                family.NormalizedLemma,
                family.PartOfSpeech,
                uncovered.Any(item => item.Lexeme.IsPhrase),
                hasMeanings ? "Ecdict" : "Missing",
                hasMeanings ? "Broad" : "Missing",
                sortOrder,
                uncovered.Select(item => item.Sense.SenseId).ToArray(),
                meanings);
        }

        private static IReadOnlyList<string> ReadExactMeanings(
            string? ili,
            IReadOnlyDictionary<string, IReadOnlyList<string>> chineseMeaningsByIli)
        {
            if (ili is null || !chineseMeaningsByIli.TryGetValue(ili, out var sourceMeanings))
            {
                return Array.Empty<string>();
            }

            return sourceMeanings
                .Where(meaning => !string.IsNullOrWhiteSpace(meaning))
                .Select(meaning => meaning.Trim().Normalize(NormalizationForm.FormC))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string NormalizeLemma(string value)
        {
            return value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        private static string ToCode(EnglishPartOfSpeech partOfSpeech)
        {
            return partOfSpeech switch
            {
                EnglishPartOfSpeech.Noun => "n",
                EnglishPartOfSpeech.Verb => "v",
                EnglishPartOfSpeech.Adjective => "a",
                EnglishPartOfSpeech.AdjectiveSatellite => "s",
                EnglishPartOfSpeech.Adverb => "r",
                _ => throw new ArgumentOutOfRangeException(nameof(partOfSpeech), partOfSpeech, "无法生成英语词性代码。")
            };
        }

        private readonly record struct IndexedLexeme(EnglishLexeme Lexeme, int Index);

        private readonly record struct SourceSense(EnglishLexeme Lexeme, EnglishSense Sense);

        private readonly record struct FamilyKey(string NormalizedLemma, EnglishPartOfSpeech PartOfSpeech);
    }
}
