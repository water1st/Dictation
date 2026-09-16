using System;
using System.Collections.Generic;
using System.Linq;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 表示 JMdict 的一个书写形式及其常用度标记。
    /// </summary>
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
}
