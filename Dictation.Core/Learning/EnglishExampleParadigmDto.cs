namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示英语示例词及其已启用词形的展示范式。
    /// </summary>
    public sealed record EnglishExampleParadigmDto(
        string SourceSenseId,
        string Headword,
        IReadOnlyList<EnglishExampleFormDto> Forms);
}
