using System;
using System.Collections.Generic;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 表示 WordNet 同义词集、ILI、定义与例句。
    /// </summary>
    internal sealed class EnglishSynset
    {
        /// <summary>
        /// 表示 WordNet 同义词集、ILI、定义与例句。
        /// </summary>
        /// <param name="synsetId">所属同义词集标识。</param>
        /// <param name="partOfSpeech">用于词形或翻译分类的英语词性。</param>
        /// <param name="definitions">同义词集定义文本。</param>
        /// <param name="examples">同义词集例句文本。</param>
        /// <param name="ili">跨语言关联标识；来源未提供时为空。</param>
        public EnglishSynset(
            string synsetId,
            EnglishPartOfSpeech partOfSpeech,
            IReadOnlyList<string> definitions,
            IReadOnlyList<string> examples,
            string? ili = null)
        {
            SynsetId = string.IsNullOrWhiteSpace(synsetId)
                ? throw new ArgumentException("synset ID 不能为空。", nameof(synsetId))
                : synsetId;
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            Examples = examples ?? throw new ArgumentNullException(nameof(examples));
            PartOfSpeech = partOfSpeech;
            Ili = string.IsNullOrWhiteSpace(ili) ? null : ili;
        }

        public string SynsetId { get; }

        public string? Ili { get; }

        public EnglishPartOfSpeech PartOfSpeech { get; }

        public IReadOnlyList<string> Definitions { get; }

        public IReadOnlyList<string> Examples { get; }
    }
}
