using Dictation.Core.Configuration;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 表示固定语言文本词表的只读候选准备结果。
    /// </summary>
    /// <param name="Language">本次词表的学习语言。</param>
    /// <param name="Lines">去重并保留首次出现顺序的来源行。</param>
    public sealed record TextImportPreparationDto(
        LearningLanguage Language,
        IReadOnlyList<TextImportLineDto> Lines);
}
