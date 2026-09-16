using System;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 表示 JMdict 的一条带语言与可选类型的释义。
    /// </summary>
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
}
