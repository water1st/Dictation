using System;
using System.Collections.Generic;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 表示 JMdict 词义及其词性、书写、读音限制和多语言释义。
    /// </summary>
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
}
