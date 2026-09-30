using Dictation.Core.Configuration;

namespace Dictation.Core.Review
{
    /// <summary>向页面与复习会话提供当前语言的独立复习计划。</summary>
    public interface IReviewPlanReader
    {
        /// <summary>读取已保存计划；首次使用只返回默认值，不写数据库。</summary>
        /// <param name="language">计划所属语言。</param>
        /// <param name="cancellationToken">取消读取和例词解析的令牌。</param>
        /// <returns>该语言的独立复习计划。</returns>
        Task<ReviewPlanDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
