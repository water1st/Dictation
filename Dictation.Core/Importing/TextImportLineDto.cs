using Dictation.Core.Lexicon;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 表示文本词表中一条去重后的源词及其精确义项候选。
    /// </summary>
    /// <param name="LineNumber">源文本中从一开始计数的行号。</param>
    /// <param name="SourceText">去除首尾空白并做 Unicode 正规化的原词。</param>
    /// <param name="Candidates">与词头或词形完全匹配的来源义项。</param>
    public sealed record TextImportLineDto(
        int LineNumber,
        string SourceText,
        IReadOnlyList<LexiconLookupResultDto> Candidates)
    {
        /// <summary>
        /// 单候选时可预选的稳定义项；多义或未匹配时必须留空。
        /// </summary>
        public string? DefaultSourceSenseId => Candidates.Count == 1
            ? Candidates[0].SourceSenseId
            : null;
    }
}
