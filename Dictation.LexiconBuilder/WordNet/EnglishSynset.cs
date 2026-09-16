using System;
using System.Collections.Generic;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 表示 WordNet 同义词集、ILI、定义与例句。
    /// </summary>
    internal sealed class EnglishSynset
    {
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
