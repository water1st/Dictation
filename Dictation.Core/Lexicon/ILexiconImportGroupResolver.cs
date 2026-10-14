using Dictation.Core.Configuration;

namespace Dictation.Core.Lexicon
{
    /// <summary>
    /// 提供按来源义项解析完整导入词组的能力。
    /// </summary>
    public interface ILexiconImportGroupResolver
    {
        /// <summary>
        /// 解析指定来源义项所属的完整导入词组。
        /// </summary>
        /// <param name="language">词库学习语言。</param>
        /// <param name="sourceSenseId">来源义项稳定标识。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>完整词组；来源义项不存在时返回空集合。</returns>
        Task<IReadOnlyList<LexiconLookupResultDto>> GetImportGroupAsync(
            LearningLanguage language,
            string sourceSenseId,
            CancellationToken cancellationToken = default);
    }
}
