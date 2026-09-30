using Dictation.Core.Configuration;

namespace Dictation.Core.Lexicon
{
    /// <summary>
    /// 描述一次按语言和数量上限执行的词库检索。
    /// </summary>
    /// <param name="Language">学习语言。</param>
    /// <param name="Query">待检索文本。</param>
    /// <param name="Limit">最多返回的结果数。</param>
    /// <param name="Offset">跳过的有序义项数，用于继续读取下一页。</param>
    /// <param name="ExactOnly">是否只检索精确词形，不包含前缀建议。</param>
    public sealed record LexiconSearchRequestDto(
        LearningLanguage Language,
        string Query,
        int Limit = 20,
        int Offset = 0,
        bool ExactOnly = false);
}
