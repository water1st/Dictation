using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dictation.LexiconBuilder;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 将 WordNet 词义与中文来源合并为稳定排序的学习词义。
    /// </summary>
    internal sealed class EnglishLearningSenseBuilder
    {
        /// <summary>
        /// 优先采用 ILI 精确中文释义，并仅为未覆盖词义生成 ECDICT 宽泛回退。
        /// </summary>
        /// <param name="lexicon">已解析且保持源顺序的 WordNet 词元与同义词集。</param>
        /// <param name="chineseMeaningsByIli">按 ILI 索引的 Chinese Open WordNet 中文释义。</param>
        /// <param name="ecdict">仅为未获精确释义的词义族提供宽泛中文回退。</param>
        /// <returns>按源词元族和词义顺序稳定排列的学习词义；无中文回退时明确标记为 Missing。</returns>
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

        /// <summary>
        /// 当没有精确中文映射时，以词元和词性级中文创建宽泛回退义项，不冒充精确义项。
        /// </summary>
        private static EnglishLearningSense CreateFallback(
            FamilyKey family,
            IReadOnlyList<SourceSense> uncovered,
            EcdictTranslationReader ecdict,
            int sortOrder)
        {
            var meanings = NormalizeChineseMeanings(
                ecdict.Read(family.NormalizedLemma, family.PartOfSpeech));
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

            return NormalizeChineseMeanings(sourceMeanings);
        }

        private static IReadOnlyList<string> NormalizeChineseMeanings(IEnumerable<string> sourceMeanings)
        {
            return sourceMeanings
                .Where(meaning => !string.IsNullOrWhiteSpace(meaning))
                .Select(meaning => meaning.Trim().Normalize(NormalizationForm.FormC))
                .Where(meaning => meaning.Any(character => character > 0x7f))
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
