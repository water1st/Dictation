using Dictation.Core.Configuration;

namespace Dictation.Core.Review
{
    /// <summary>按独立复习计划启动或恢复同语言的客观复习会话。</summary>
    public interface IReviewSessionStarter
    {
        /// <summary>优先恢复活动会话；无待测／到期候选或额度用完时返回空。</summary>
        /// <param name="language">当前首页选定的语言。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        Task<ReviewSessionDto?> StartOrResumeAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
