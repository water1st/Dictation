using System;

namespace Dictation.LexiconBuilder.JmDict
{
    /// <summary>
    /// 表示 JMdict 的一条带语言与可选类型的释义。
    /// </summary>
    internal sealed class JmDictGloss
    {
        /// <summary>
        /// 表示 JMdict 的一条带语言与可选类型的释义。
        /// </summary>
        /// <param name="language">来源释义的语言代码。</param>
        /// <param name="text">来源记录的文本内容。</param>
        /// <param name="type">来源释义类型；未标记时为空。</param>
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
