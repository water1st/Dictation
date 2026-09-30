using Dictation.Core.Configuration;
using Dictation.Core.Importing;

namespace Dictation.Core.Review
{
    /// <summary>只读取活动复习会话及本轮完整词组，不改变记忆或答题状态。</summary>
    public interface IReviewSessionReader
    {
        /// <summary>读取指定语言的未完成会话。</summary>
        /// <param name="language">待查询的语言。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        Task<ReviewSessionDto?> GetActiveAsync(LearningLanguage language, CancellationToken cancellationToken = default);

        /// <summary>按会话词组顺序读取中文释义、属性与可用词形。</summary>
        /// <param name="sessionId">要浏览的复习会话标识。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        Task<IReadOnlyList<ImportWordGroupDto>> GetBrowseItemsAsync(string sessionId, CancellationToken cancellationToken = default);
    }
}
