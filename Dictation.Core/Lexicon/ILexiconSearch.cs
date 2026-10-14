namespace Dictation.Core.Lexicon
{
    /// <summary>
    /// 提供按学习语言检索词库的能力。
    /// </summary>
    public interface ILexiconSearch
    {
        /// <summary>
        /// 按请求中的语言、查询文本和数量上限检索词库。
        /// </summary>
        /// <param name="request">词库检索请求。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>匹配的词库结果。</returns>
        Task<IReadOnlyList<LexiconLookupResultDto>> SearchAsync(
            LexiconSearchRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
