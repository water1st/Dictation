using System;
using System.Collections.Generic;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 表示一个英语词元及其源词形、词义和短语标记。
    /// </summary>
    internal sealed class EnglishLexeme
    {
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
