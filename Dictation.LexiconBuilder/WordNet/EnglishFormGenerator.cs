using System;
using System.Collections.Generic;
using System.Linq;

using Dictation.LexiconBuilder;

namespace Dictation.LexiconBuilder.WordNet
{
    internal sealed class EnglishFormGenerator
    {
        internal IReadOnlyList<EnglishForm> Generate(EnglishLexeme lexeme)
        {
            ArgumentNullException.ThrowIfNull(lexeme);
            if (lexeme.PartOfSpeech == EnglishPartOfSpeech.Unknown)
            {
                throw new LexiconBuildException($"无法为词条 {lexeme.Lemma} 生成未知词性 {lexeme.SourceEntryKey} 的词形。");
            }

            var forms = new Dictionary<FormKey, EnglishForm>();
            AddForm(forms, lexeme.Lemma, EnglishFormKind.Base, EnglishFormSourceKind.Source);
            if (lexeme.IsPhrase)
            {
                return SortForms(forms);
            }

            var ruleForms = BuildRuleForms(lexeme.Lemma, lexeme.PartOfSpeech);
            var sourceKinds = new HashSet<EnglishFormKind>();
            foreach (var sourceForm in lexeme.SourceForms)
            {
                var kinds = ClassifySourceForm(lexeme, sourceForm, ruleForms);
                foreach (var kind in kinds)
                {
                    sourceKinds.Add(kind);
                    AddForm(forms, sourceForm, kind, EnglishFormSourceKind.Source);
                }
            }

            foreach (var ruleForm in ruleForms)
            {
                if (!sourceKinds.Contains(ruleForm.Kind))
                {
                    AddForm(forms, ruleForm.Surface, ruleForm.Kind, EnglishFormSourceKind.Rule);
                }
            }

            return SortForms(forms);
        }

        private static IReadOnlyList<EnglishFormKind> ClassifySourceForm(
            EnglishLexeme lexeme,
            string sourceForm,
            IReadOnlyList<EnglishForm> ruleForms)
        {
            var ruleKinds = ruleForms
                .Where(form => string.Equals(form.Surface, sourceForm, StringComparison.Ordinal))
                .Select(form => form.Kind)
                .ToArray();
            if (ruleKinds.Length > 0)
            {
                return ruleKinds;
            }

            if (string.Equals(lexeme.Lemma, sourceForm, StringComparison.Ordinal))
            {
                return new[] { EnglishFormKind.Base };
            }

            if (EnglishIrregularParadigms.TryGetKinds(
                lexeme.Lemma,
                lexeme.PartOfSpeech,
                sourceForm,
                out var irregularKinds))
            {
                return irregularKinds;
            }

            if (lexeme.PartOfSpeech == EnglishPartOfSpeech.Verb)
            {
                if (sourceForm.EndsWith("ing", StringComparison.Ordinal))
                {
                    return new[] { EnglishFormKind.PresentParticiple };
                }

                if (sourceForm.EndsWith("ed", StringComparison.Ordinal))
                {
                    return new[] { EnglishFormKind.Past, EnglishFormKind.PastParticiple };
                }

                if (sourceForm.EndsWith("ies", StringComparison.Ordinal)
                    || sourceForm.EndsWith("es", StringComparison.Ordinal)
                    || sourceForm.EndsWith("s", StringComparison.Ordinal))
                {
                    return new[] { EnglishFormKind.ThirdPersonSingular };
                }
            }

            if (lexeme.PartOfSpeech == EnglishPartOfSpeech.Noun)
            {
                return new[] { EnglishFormKind.Plural };
            }

            if ((lexeme.PartOfSpeech == EnglishPartOfSpeech.Adjective
                    || lexeme.PartOfSpeech == EnglishPartOfSpeech.Adverb)
                && (sourceForm.EndsWith("er", StringComparison.Ordinal)
                    || sourceForm.EndsWith("est", StringComparison.Ordinal)))
            {
                return sourceForm.EndsWith("est", StringComparison.Ordinal)
                    ? new[] { EnglishFormKind.Superlative }
                    : new[] { EnglishFormKind.Comparative };
            }

            throw new LexiconBuildException(
                $"无法分类英语来源词形：lemma={lexeme.Lemma}, POS={ToCode(lexeme.PartOfSpeech)}, form={sourceForm}。");
        }

        private static IReadOnlyList<EnglishForm> BuildRuleForms(
            string lemma,
            EnglishPartOfSpeech partOfSpeech)
        {
            var forms = new List<EnglishForm>();
            var paradigmKinds = new HashSet<EnglishFormKind>();
            if (EnglishIrregularParadigms.TryGetParadigm(lemma, partOfSpeech, out var paradigm))
            {
                foreach (var pair in paradigm)
                {
                    paradigmKinds.Add(pair.Key);
                    foreach (var surface in pair.Value)
                    {
                        forms.Add(new EnglishForm(surface, pair.Key, EnglishFormSourceKind.Rule));
                    }
                }
            }

            switch (partOfSpeech)
            {
                case EnglishPartOfSpeech.Verb:
                {
                    AddRuleFormIfMissing(forms, paradigmKinds, AddThirdPersonSingular(lemma), EnglishFormKind.ThirdPersonSingular);
                    AddRuleFormIfMissing(forms, paradigmKinds, AddPast(lemma), EnglishFormKind.Past);
                    AddRuleFormIfMissing(forms, paradigmKinds, AddPast(lemma), EnglishFormKind.PastParticiple);
                    AddRuleFormIfMissing(forms, paradigmKinds, AddPresentParticiple(lemma), EnglishFormKind.PresentParticiple);
                    break;
                }
                case EnglishPartOfSpeech.Noun:
                {
                    AddRuleFormIfMissing(forms, paradigmKinds, AddPlural(lemma), EnglishFormKind.Plural);
                    break;
                }
                default:
                {
                    break;
                }
            }

            return forms;
        }

        private static void AddRuleFormIfMissing(
            ICollection<EnglishForm> forms,
            ISet<EnglishFormKind> paradigmKinds,
            string surface,
            EnglishFormKind kind)
        {
            if (!paradigmKinds.Contains(kind))
            {
                forms.Add(new EnglishForm(surface, kind, EnglishFormSourceKind.Rule));
            }
        }

        private static string AddThirdPersonSingular(string lemma)
        {
            if (lemma.EndsWith("y", StringComparison.Ordinal)
                && lemma.Length > 1
                && !IsVowel(lemma[^2]))
            {
                return lemma[..^1] + "ies";
            }

            return lemma.EndsWith("s", StringComparison.Ordinal)
                || lemma.EndsWith("x", StringComparison.Ordinal)
                || lemma.EndsWith("z", StringComparison.Ordinal)
                || lemma.EndsWith("ch", StringComparison.Ordinal)
                || lemma.EndsWith("sh", StringComparison.Ordinal)
                || lemma.EndsWith("o", StringComparison.Ordinal)
                ? lemma + "es"
                : lemma + "s";
        }

        private static string AddPast(string lemma)
        {
            if (lemma.EndsWith("e", StringComparison.Ordinal))
            {
                return lemma + "d";
            }

            if (lemma.EndsWith("y", StringComparison.Ordinal)
                && lemma.Length > 1
                && !IsVowel(lemma[^2]))
            {
                return lemma[..^1] + "ied";
            }

            return lemma + "ed";
        }

        private static string AddPresentParticiple(string lemma)
        {
            if (lemma.EndsWith("ie", StringComparison.Ordinal))
            {
                return lemma[..^2] + "ying";
            }

            if (lemma.EndsWith("e", StringComparison.Ordinal)
                && !lemma.EndsWith("ee", StringComparison.Ordinal)
                && !lemma.EndsWith("ye", StringComparison.Ordinal)
                && !lemma.EndsWith("oe", StringComparison.Ordinal))
            {
                return lemma[..^1] + "ing";
            }

            return lemma + "ing";
        }

        private static string AddPlural(string lemma)
        {
            if (lemma.EndsWith("y", StringComparison.Ordinal)
                && lemma.Length > 1
                && !IsVowel(lemma[^2]))
            {
                return lemma[..^1] + "ies";
            }

            return lemma.EndsWith("s", StringComparison.Ordinal)
                || lemma.EndsWith("x", StringComparison.Ordinal)
                || lemma.EndsWith("z", StringComparison.Ordinal)
                || lemma.EndsWith("ch", StringComparison.Ordinal)
                || lemma.EndsWith("sh", StringComparison.Ordinal)
                || lemma.EndsWith("o", StringComparison.Ordinal)
                ? lemma + "es"
                : lemma + "s";
        }

        private static bool IsVowel(char value)
        {
            return value is 'a' or 'e' or 'i' or 'o' or 'u';
        }

        private static void AddForm(
            IDictionary<FormKey, EnglishForm> forms,
            string surface,
            EnglishFormKind kind,
            EnglishFormSourceKind sourceKind)
        {
            var key = new FormKey(surface, kind);
            if (!forms.TryGetValue(key, out var existing)
                || (sourceKind == EnglishFormSourceKind.Source && existing.SourceKind == EnglishFormSourceKind.Rule))
            {
                forms[key] = new EnglishForm(surface, kind, sourceKind);
            }
        }

        private static IReadOnlyList<EnglishForm> SortForms(IReadOnlyDictionary<FormKey, EnglishForm> forms)
        {
            return forms.Values
                .OrderBy(form => form.Kind)
                .ThenBy(form => form.Surface, StringComparer.Ordinal)
                .ThenBy(form => form.SourceKind)
                .ToArray();
        }

        private static string ToCode(EnglishPartOfSpeech partOfSpeech)
        {
            return partOfSpeech switch
            {
                EnglishPartOfSpeech.Noun => "n",
                EnglishPartOfSpeech.Verb => "v",
                EnglishPartOfSpeech.Adjective => "a",
                EnglishPartOfSpeech.Adverb => "r",
                _ => "unknown"
            };
        }

        private readonly record struct FormKey(string Surface, EnglishFormKind Kind);
    }
}
