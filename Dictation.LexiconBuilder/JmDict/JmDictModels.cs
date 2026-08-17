using System;
using System.Collections.Generic;
using System.Linq;

namespace Dictation.LexiconBuilder.JmDict
{
    internal sealed class JmDictEntry
    {
        public JmDictEntry(
            int sequence,
            IReadOnlyList<JmDictOrthography> orthographies,
            IReadOnlyList<JmDictReading> readings,
            IReadOnlyList<JmDictSense> senses)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Sequence = sequence;
            Orthographies = orthographies ?? throw new ArgumentNullException(nameof(orthographies));
            Readings = readings ?? throw new ArgumentNullException(nameof(readings));
            Senses = senses ?? throw new ArgumentNullException(nameof(senses));
        }

        public int Sequence { get; }

        public IReadOnlyList<JmDictOrthography> Orthographies { get; }

        public IReadOnlyList<JmDictReading> Readings { get; }

        public IReadOnlyList<JmDictSense> Senses { get; }
    }

    internal sealed class JmDictOrthography
    {
        public JmDictOrthography(string text, IReadOnlyList<string> priorities)
        {
            Text = RequireText(text, nameof(text));
            Priorities = priorities ?? throw new ArgumentNullException(nameof(priorities));
        }

        public string Text { get; }

        public IReadOnlyList<string> Priorities { get; }

        public bool IsCommon => Priorities.Any(JmDictPriority.IsCommon);

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("文本不能为空。", parameterName)
                : value;
        }
    }

    internal sealed class JmDictReading
    {
        public JmDictReading(
            string text,
            IReadOnlyList<string> priorities,
            IReadOnlyList<string> restrictions,
            bool noKanji)
        {
            Text = string.IsNullOrWhiteSpace(text)
                ? throw new ArgumentException("文本不能为空。", nameof(text))
                : text;
            Priorities = priorities ?? throw new ArgumentNullException(nameof(priorities));
            Restrictions = restrictions ?? throw new ArgumentNullException(nameof(restrictions));
            NoKanji = noKanji;
        }

        public string Text { get; }

        public IReadOnlyList<string> Priorities { get; }

        public IReadOnlyList<string> Restrictions { get; }

        public bool NoKanji { get; }

        public bool IsCommon => Priorities.Any(JmDictPriority.IsCommon);
    }

    internal sealed class JmDictSense
    {
        public JmDictSense(
            int index,
            IReadOnlyList<string> partsOfSpeech,
            IReadOnlyList<string> kanjiRestrictions,
            IReadOnlyList<string> readingRestrictions,
            IReadOnlyList<JmDictGloss> glosses)
        {
            if (index <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Index = index;
            PartsOfSpeech = partsOfSpeech ?? throw new ArgumentNullException(nameof(partsOfSpeech));
            KanjiRestrictions = kanjiRestrictions ?? throw new ArgumentNullException(nameof(kanjiRestrictions));
            ReadingRestrictions = readingRestrictions ?? throw new ArgumentNullException(nameof(readingRestrictions));
            Glosses = glosses ?? throw new ArgumentNullException(nameof(glosses));
        }

        public int Index { get; }

        public IReadOnlyList<string> PartsOfSpeech { get; }

        public IReadOnlyList<string> KanjiRestrictions { get; }

        public IReadOnlyList<string> ReadingRestrictions { get; }

        public IReadOnlyList<JmDictGloss> Glosses { get; }
    }

    internal sealed class JmDictGloss
    {
        public JmDictGloss(string language, string text, string? type = null)
        {
            Language = string.IsNullOrWhiteSpace(language) ? "eng" : language;
            Text = string.IsNullOrWhiteSpace(text)
                ? throw new ArgumentException("释义不能为空。", nameof(text))
                : text;
            Type = type;
        }

        public string Language { get; }

        public string Text { get; }

        public string? Type { get; }
    }

    internal static class JmDictPriority
    {
        public static bool IsCommon(string priority)
        {
            return priority.StartsWith("news", StringComparison.OrdinalIgnoreCase)
                || priority.StartsWith("ichi", StringComparison.OrdinalIgnoreCase)
                || priority.StartsWith("spec", StringComparison.OrdinalIgnoreCase)
                || priority.StartsWith("gai", StringComparison.OrdinalIgnoreCase)
                || priority.StartsWith("nf", StringComparison.OrdinalIgnoreCase);
        }
    }
}
