using System;
using System.Collections.Generic;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 聚合一个 JMdict 条目的正字法、读音与词义。
    /// </summary>
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
}
