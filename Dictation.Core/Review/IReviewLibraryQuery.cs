using Dictation.Core.Configuration;

namespace Dictation.Core.Review
{
    /// <summary>向 UI 和其它入口提供只读风险复习词库能力，不包含答题或修改操作。</summary>
    public interface IReviewLibraryQuery
    {
        /// <summary>按当前风险返回指定语言的整组词条及记忆信息。</summary>
        /// <param name="language">所查看的语言。</param>
        /// <param name="cancellationToken">取消查询的令牌。</param>
        Task<ReviewLibraryDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
