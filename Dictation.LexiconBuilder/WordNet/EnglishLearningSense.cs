using System.Collections.Generic;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 表示写入英语学习词库的归并词义及其中文释义来源精度。
    /// </summary>
    internal sealed record EnglishLearningSense(
        string LearningSenseId,
        string SourceLexemeId,
        string Headword,
        EnglishPartOfSpeech PartOfSpeech,
        bool IsPhrase,
        string MeaningSource,
        string MeaningPrecision,
        int SortOrder,
        IReadOnlyList<string> SourceSenseIds,
        IReadOnlyList<string> Meanings);
}
