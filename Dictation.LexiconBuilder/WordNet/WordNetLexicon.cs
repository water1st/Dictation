using System;
using System.Collections.Generic;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 聚合一次解析得到的 WordNet 词元与同义词集。
    /// </summary>
    internal sealed class WordNetLexicon
    {
        /// <summary>
        /// 聚合一次解析得到的 WordNet 词元与同义词集。
        /// </summary>
        /// <param name="lexemes">解析得到的词元集合。</param>
        /// <param name="synsets">解析得到的同义词集集合。</param>
        public WordNetLexicon(
            IReadOnlyList<EnglishLexeme> lexemes,
            IReadOnlyList<EnglishSynset> synsets)
        {
            Lexemes = lexemes ?? throw new ArgumentNullException(nameof(lexemes));
            Synsets = synsets ?? throw new ArgumentNullException(nameof(synsets));
        }

        public IReadOnlyList<EnglishLexeme> Lexemes { get; }

        public IReadOnlyList<EnglishSynset> Synsets { get; }
    }
}
