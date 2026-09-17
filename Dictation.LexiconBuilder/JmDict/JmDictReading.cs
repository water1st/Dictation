using System;
using System.Collections.Generic;
using System.Linq;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 表示 JMdict 读音、适用书写限制与常用度标记。
    /// </summary>
    internal sealed class JmDictReading
    {
        /// <summary>
        /// 表示 JMdict 读音、适用书写限制与常用度标记。
        /// </summary>
        /// <param name="text">来源记录的文本内容。</param>
        /// <param name="priorities">来源提供的常用度和频率标记。</param>
        /// <param name="restrictions">读音适用的书写形式限制。</param>
        /// <param name="noKanji">是否明确不对应汉字书写。</param>
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
}
