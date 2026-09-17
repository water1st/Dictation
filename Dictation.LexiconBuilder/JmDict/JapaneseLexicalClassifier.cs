using System;
using System.Collections.Generic;
using System.Linq;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 将 JMdict 词性标签归并为学习词库所需的类别、角色与动词活用类。
    /// </summary>
    internal static class JapaneseLexicalClassifier
    {
        private const string NounOrSuruPos = "noun or participle which takes the aux. verb suru";

        /// <summary>
        /// 按 JMdict 标签优先级确定词汇类别，并保留名词兼动词等多角色语义。
        /// </summary>
        /// <param name="partsOfSpeech">JMdict 当前词义的原始词性标签。</param>
        /// <returns>用于展示和词形生成的主类别、全部角色与动词活用类。</returns>
        public static JapaneseLexicalClassification Classify(IReadOnlyList<string> partsOfSpeech)
        {
            if (partsOfSpeech.Count == 0)
            {
                return Create(JapaneseLexicalCategory.Other, JapaneseLexicalRole.Other, JapaneseVerbClass.None);
            }

            if (partsOfSpeech.Any(IsPhrase))
            {
                return Create(JapaneseLexicalCategory.Phrase, JapaneseLexicalRole.Other, JapaneseVerbClass.None);
            }

            var isNoun = partsOfSpeech.Any(IsNoun);
            var isNounVerb = partsOfSpeech.Any(IsNounOrSuru);
            var isVerb = partsOfSpeech.Any(IsVerb);
            var isIAdjective = partsOfSpeech.Any(IsIAdjective);
            var isNaAdjective = partsOfSpeech.Any(IsNaAdjective);
            var isAdverb = partsOfSpeech.Any(IsAdverb);
            var verbClass = isVerb || isNounVerb
                ? ClassifyVerb(partsOfSpeech)
                : JapaneseVerbClass.None;

            var roles = new List<JapaneseLexicalRole>();
            if (isNoun)
            {
                roles.Add(JapaneseLexicalRole.Noun);
            }

            if (isVerb)
            {
                roles.Add(JapaneseLexicalRole.Verb);
            }

            if (isIAdjective || isNaAdjective)
            {
                roles.Add(JapaneseLexicalRole.Adjective);
            }

            if (isAdverb)
            {
                roles.Add(JapaneseLexicalRole.Adverb);
            }

            if (roles.Count == 0)
            {
                roles.Add(JapaneseLexicalRole.Other);
            }

            var category = isNounVerb || (isNoun && isVerb)
                ? JapaneseLexicalCategory.NounVerb
                : isVerb
                    ? JapaneseLexicalCategory.Verb
                    : isIAdjective
                        ? JapaneseLexicalCategory.IAdjective
                        : isNaAdjective
                            ? JapaneseLexicalCategory.NaAdjective
                            : isAdverb
                                ? JapaneseLexicalCategory.Adverb
                                : isNoun
                                    ? JapaneseLexicalCategory.Noun
                                    : JapaneseLexicalCategory.Other;
            return new JapaneseLexicalClassification(category, roles, verbClass);
        }

        private static JapaneseLexicalClassification Create(
            JapaneseLexicalCategory category,
            JapaneseLexicalRole role,
            JapaneseVerbClass verbClass)
        {
            return new JapaneseLexicalClassification(category, new[] { role }, verbClass);
        }

        private static bool IsPhrase(string partOfSpeech)
        {
            return partOfSpeech.Contains("Expressions (phrases", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNoun(string partOfSpeech)
        {
            var normalized = partOfSpeech.Trim();
            if (normalized.Contains("adjectival noun", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return normalized.StartsWith("adverbial noun", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("noun", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(" noun", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNounOrSuru(string partOfSpeech)
        {
            return string.Equals(partOfSpeech.Trim(), NounOrSuruPos, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVerb(string partOfSpeech)
        {
            return ContainsWord(partOfSpeech, "verb");
        }

        private static bool IsIAdjective(string partOfSpeech)
        {
            return ContainsWord(partOfSpeech, "i-adjective")
                || partOfSpeech.Contains("adjective (keiyoushi)", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNaAdjective(string partOfSpeech)
        {
            return ContainsWord(partOfSpeech, "na-adjective")
                || partOfSpeech.Contains("adjectival noun", StringComparison.OrdinalIgnoreCase)
                || partOfSpeech.Contains("adjectival nouns or quasi-adjectives", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAdverb(string partOfSpeech)
        {
            return ContainsWord(partOfSpeech, "adverb")
                || partOfSpeech.StartsWith("adverbial noun", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsWord(string value, string word)
        {
            var startIndex = 0;
            while ((startIndex = value.IndexOf(word, startIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var endIndex = startIndex + word.Length;
                var hasWordCharacterBefore = startIndex > 0 && char.IsLetterOrDigit(value[startIndex - 1]);
                var hasWordCharacterAfter = endIndex < value.Length && char.IsLetterOrDigit(value[endIndex]);
                if (!hasWordCharacterBefore && !hasWordCharacterAfter)
                {
                    return true;
                }

                startIndex = endIndex;
            }

            return false;
        }

        private static JapaneseVerbClass ClassifyVerb(IReadOnlyList<string> partsOfSpeech)
        {
            foreach (var rawPartOfSpeech in partsOfSpeech)
            {
                var partOfSpeech = rawPartOfSpeech.Trim();
                if (partOfSpeech.Contains("zuru verb", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.Zuru;
                }

                if (partOfSpeech.Contains("Ichidan verb", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.Ichidan;
                }

                if (partOfSpeech.Contains("Kuru verb", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.Kuru;
                }

                if (partOfSpeech.Contains("Suru verb", StringComparison.OrdinalIgnoreCase)
                    || IsNounOrSuru(partOfSpeech))
                {
                    return JapaneseVerbClass.Suru;
                }

                if (partOfSpeech.Contains("Godan verb - iku/yuku", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.Iku;
                }

                if (partOfSpeech.Contains("Godan verb - -aru special class", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.Aru;
                }

                if (partOfSpeech.Contains("Godan verb with 'u' ending (special", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.USpecial;
                }

                if (partOfSpeech.Contains("Godan verb with 'u' ending", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.GodanU;
                }

                if (partOfSpeech.Contains("Godan verb with 'ku' ending", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.GodanK;
                }

                if (partOfSpeech.Contains("Godan verb with 'gu' ending", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.GodanG;
                }

                if (partOfSpeech.Contains("Godan verb with 'su' ending", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.GodanS;
                }

                if (partOfSpeech.Contains("Godan verb with 'tsu' ending", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.GodanT;
                }

                if (partOfSpeech.Contains("Godan verb with 'nu' ending", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.GodanN;
                }

                if (partOfSpeech.Contains("Godan verb with 'bu' ending", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.GodanB;
                }

                if (partOfSpeech.Contains("Godan verb with 'mu' ending", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.GodanM;
                }

                if (partOfSpeech.Contains("Godan verb with 'ru' ending", StringComparison.OrdinalIgnoreCase))
                {
                    return JapaneseVerbClass.GodanR;
                }
            }

            return JapaneseVerbClass.None;
        }
    }
}
