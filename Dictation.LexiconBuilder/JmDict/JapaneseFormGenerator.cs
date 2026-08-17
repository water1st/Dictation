using System;
using System.Collections.Generic;
using System.Linq;

namespace Dictation.LexiconBuilder.JmDict
{
    internal enum JapaneseFormKind
    {
        Dictionary = 0,
        Polite = 1,
        Te = 2,
        Ta = 3
    }

    internal enum JapaneseScriptKind
    {
        Kanji = 0,
        Kana = 1
    }

    internal sealed class JapaneseLexiconForm
    {
        public JapaneseLexiconForm(
            string surface,
            string reading,
            JapaneseFormKind formKind,
            JapaneseScriptKind scriptKind,
            JapaneseLexicalRole lexicalRole,
            bool isCommon,
            bool isPrimary)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("词形不能为空。", nameof(surface))
                : surface;
            Reading = string.IsNullOrWhiteSpace(reading)
                ? throw new ArgumentException("读音不能为空。", nameof(reading))
                : reading;
            FormKind = formKind;
            ScriptKind = scriptKind;
            LexicalRole = lexicalRole;
            IsCommon = isCommon;
            IsPrimary = isPrimary;
        }

        public string Surface { get; }

        public string Reading { get; }

        public JapaneseFormKind FormKind { get; }

        public JapaneseScriptKind ScriptKind { get; }

        public JapaneseLexicalRole LexicalRole { get; }

        public bool IsCommon { get; }

        public bool IsPrimary { get; }
    }

    internal sealed class JapaneseFormGenerator
    {
        private static readonly IReadOnlyDictionary<JapaneseVerbClass, JapaneseVerbRule> VerbRules =
            new Dictionary<JapaneseVerbClass, JapaneseVerbRule>
            {
                [JapaneseVerbClass.Ichidan] = new("る", "ます", "て", "た"),
                [JapaneseVerbClass.GodanU] = new("う", "います", "って", "った"),
                [JapaneseVerbClass.GodanK] = new("く", "きます", "いて", "いた"),
                [JapaneseVerbClass.GodanG] = new("ぐ", "ぎます", "いで", "いだ"),
                [JapaneseVerbClass.GodanS] = new("す", "します", "して", "した"),
                [JapaneseVerbClass.GodanT] = new("つ", "ちます", "って", "った"),
                [JapaneseVerbClass.GodanN] = new("ぬ", "にます", "んで", "んだ"),
                [JapaneseVerbClass.GodanB] = new("ぶ", "びます", "んで", "んだ"),
                [JapaneseVerbClass.GodanM] = new("む", "みます", "んで", "んだ"),
                [JapaneseVerbClass.GodanR] = new("る", "ります", "って", "った"),
                [JapaneseVerbClass.Iku] = new("く", "きます", "って", "った"),
                [JapaneseVerbClass.USpecial] = new("う", "います", "うて", "うた"),
                [JapaneseVerbClass.Suru] = new("する", "します", "して", "した"),
                [JapaneseVerbClass.Zuru] = new("ずる", "じます", "じて", "じた"),
                [JapaneseVerbClass.Aru] = new("る", "います", "って", "った"),
                [JapaneseVerbClass.Kuru] = new("くる", "きます", "きて", "きた")
            };

        internal IReadOnlyList<JapaneseLexiconForm> Generate(JmDictEntry entry, JmDictSense sense)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(sense);

            var classification = JapaneseLexicalClassifier.Classify(sense.PartsOfSpeech);
            var forms = new Dictionary<FormKey, JapaneseLexiconForm>();
            var hasPrimary = false;

            foreach (var reading in entry.Readings)
            {
                if (sense.ReadingRestrictions.Count > 0
                    && !sense.ReadingRestrictions.Contains(reading.Text, StringComparer.Ordinal))
                {
                    continue;
                }

                var orthographies = entry.Orthographies
                    .Where(orthography => !reading.NoKanji)
                    .Where(orthography => reading.Restrictions.Count == 0
                        || reading.Restrictions.Contains(orthography.Text, StringComparer.Ordinal))
                    .Where(orthography => sense.KanjiRestrictions.Count == 0
                        || sense.KanjiRestrictions.Contains(orthography.Text, StringComparer.Ordinal))
                    .ToArray();

                var readingIsPrimary = !hasPrimary;
                if (orthographies.Length == 0)
                {
                    AddSourceForms(
                        forms,
                        reading.Text,
                        reading.Text,
                        reading.IsCommon,
                        readingIsPrimary,
                        classification,
                        JapaneseScriptKind.Kana);
                    hasPrimary |= readingIsPrimary;
                    continue;
                }

                var firstOrthography = true;
                foreach (var orthography in orthographies)
                {
                    var isPrimary = readingIsPrimary && firstOrthography;
                    var isCommon = orthography.IsCommon || reading.IsCommon;
                    AddSourceForms(
                        forms,
                        orthography.Text,
                        reading.Text,
                        isCommon,
                        isPrimary,
                        classification,
                        JapaneseScriptKind.Kanji);
                    AddSourceForms(
                        forms,
                        reading.Text,
                        reading.Text,
                        reading.IsCommon,
                        readingIsPrimary,
                        classification,
                        JapaneseScriptKind.Kana);
                    firstOrthography = false;
                    hasPrimary |= isPrimary;
                }
            }

            return forms.Values
                .OrderBy(form => form.FormKind)
                .ThenBy(form => form.LexicalRole)
                .ThenByDescending(form => form.IsPrimary)
                .ThenByDescending(form => form.IsCommon)
                .ThenBy(form => form.Surface, StringComparer.Ordinal)
                .ThenBy(form => form.Reading, StringComparer.Ordinal)
                .ThenBy(form => form.ScriptKind)
                .ToArray();
        }

        private static void AddSourceForms(
            IDictionary<FormKey, JapaneseLexiconForm> forms,
            string sourceSurface,
            string sourceReading,
            bool isCommon,
            bool isPrimary,
            JapaneseLexicalClassification classification,
            JapaneseScriptKind scriptKind)
        {
            foreach (var role in GetRoles(classification))
            {
                var roleSurface = sourceSurface;
                var roleReading = sourceReading;
                if (role == JapaneseLexicalRole.Verb && classification.Category == JapaneseLexicalCategory.NounVerb)
                {
                    roleSurface = AddSuruSuffix(roleSurface);
                    roleReading = AddSuruSuffix(roleReading);
                }

                var formKinds = classification.VerbClass == JapaneseVerbClass.None || role != JapaneseLexicalRole.Verb
                    ? new[] { JapaneseFormKind.Dictionary }
                    : Enum.GetValues<JapaneseFormKind>();
                foreach (var formKind in formKinds)
                {
                    var surface = Inflect(roleSurface, classification.VerbClass, formKind);
                    var reading = Inflect(roleReading, classification.VerbClass, formKind);
                    if (surface is null || reading is null)
                    {
                        continue;
                    }

                    var form = new JapaneseLexiconForm(
                        surface,
                        reading,
                        formKind,
                        scriptKind,
                        role,
                        isCommon,
                        isPrimary);
                    var key = new FormKey(surface, reading, formKind, scriptKind, role);
                    if (!forms.TryGetValue(key, out var existing)
                        || (isPrimary && !existing.IsPrimary)
                        || (isCommon && !existing.IsCommon))
                    {
                        forms[key] = form;
                    }
                }
            }
        }

        private static IReadOnlyList<JapaneseLexicalRole> GetRoles(JapaneseLexicalClassification classification)
        {
            return classification.Roles.Count == 0
                ? new[] { JapaneseLexicalRole.Other }
                : classification.Roles;
        }

        private static string AddSuruSuffix(string source)
        {
            return source.EndsWith("する", StringComparison.Ordinal) ? source : source + "する";
        }

        private static string? Inflect(string source, JapaneseVerbClass verbClass, JapaneseFormKind formKind)
        {
            if (formKind == JapaneseFormKind.Dictionary || verbClass == JapaneseVerbClass.None)
            {
                return source;
            }

            var rule = VerbRules[verbClass];
            if (verbClass == JapaneseVerbClass.Kuru && source.EndsWith("来る", StringComparison.Ordinal))
            {
                rule = new JapaneseVerbRule("る", "ます", "て", "た");
            }
            else if (verbClass == JapaneseVerbClass.Aru && source.EndsWith("ある", StringComparison.Ordinal))
            {
                rule = new JapaneseVerbRule("ある", "あります", "あって", "あった");
            }

            if (!source.EndsWith(rule.Dictionary, StringComparison.Ordinal))
            {
                return null;
            }

            var replacement = formKind switch
            {
                JapaneseFormKind.Polite => rule.Polite,
                JapaneseFormKind.Te => rule.Te,
                JapaneseFormKind.Ta => rule.Ta,
                _ => rule.Dictionary
            };
            return source[..^rule.Dictionary.Length] + replacement;
        }

        private readonly record struct FormKey(
            string Surface,
            string Reading,
            JapaneseFormKind FormKind,
            JapaneseScriptKind ScriptKind,
            JapaneseLexicalRole LexicalRole);

        private readonly record struct JapaneseVerbRule(
            string Dictionary,
            string Polite,
            string Te,
            string Ta);
    }
}
