using System;
using System.Collections.Generic;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 表示一个英语词元及其源词形、词义和短语标记。
    /// </summary>
    internal sealed class EnglishLexeme
    {
        /// <summary>
        /// 表示一个英语词元及其源词形、词义和短语标记。
        /// </summary>
        /// <param name="lexemeId">稳定来源词元标识。</param>
        /// <param name="lemma">要查找的规范化英语词元。</param>
        /// <param name="sourceEntryKey">WordNet 来源条目的原始键。</param>
        /// <param name="partOfSpeech">用于词形或翻译分类的英语词性。</param>
        /// <param name="sourceForms">来源显式列出的补充词形，不代表完整范式。</param>
        /// <param name="senses">按来源顺序排列的义项。</param>
        /// <param name="isPhrase">是否为多词短语。</param>
        public EnglishLexeme(
            string lexemeId,
            string lemma,
            string sourceEntryKey,
            EnglishPartOfSpeech partOfSpeech,
            IReadOnlyList<string> sourceForms,
            IReadOnlyList<EnglishSense> senses,
            bool isPhrase)
        {
            LexemeId = RequireText(lexemeId, nameof(lexemeId));
            Lemma = RequireText(lemma, nameof(lemma));
            SourceEntryKey = RequireText(sourceEntryKey, nameof(sourceEntryKey));
            SourceForms = sourceForms ?? throw new ArgumentNullException(nameof(sourceForms));
            Senses = senses ?? throw new ArgumentNullException(nameof(senses));
            PartOfSpeech = partOfSpeech;
            IsPhrase = isPhrase;
        }

        public string LexemeId { get; }

        public string Lemma { get; }

        public string SourceEntryKey { get; }

        public EnglishPartOfSpeech PartOfSpeech { get; }

        public IReadOnlyList<string> SourceForms { get; }

        public IReadOnlyList<EnglishSense> Senses { get; }

        public bool IsPhrase { get; }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("WordNet 文本不能为空。", parameterName)
                : value;
        }
    }
}
