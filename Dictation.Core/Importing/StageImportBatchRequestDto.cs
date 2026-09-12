using Dictation.Core.Configuration;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 描述把一组词库选择暂存为导入批次的请求。
    /// </summary>
    /// <param name="Language">本批次的学习语言。</param>
    /// <param name="SourceKind">本批次的来源渠道。</param>
    /// <param name="Selections">需要解析并暂存的义项选择。</param>
    public sealed record StageImportBatchRequestDto(
        LearningLanguage Language,
        ImportSourceKind SourceKind,
        IReadOnlyList<ImportSelectionDto> Selections);
}
