using Dictation.Core.Configuration;
using Dictation.Core.Review;

namespace Dictation.Core.Mistakes
{
    /// <summary>提供用户主动重练错误形式的启动边界，后续作答仍走原复习能力。</summary>
    public interface IWrongAnswerPractice
    {
        /// <summary>优先恢复活动复习；否则为选中复习组的启用错误形式建立原复习会话。</summary>
        /// <param name="language">页面固定语言。</param>
        /// <param name="wordGroupIds">用户选中的不同复习整组标识；不能传入学习中词组。</param>
        /// <param name="cancellationToken">取消启动的令牌。</param>
        Task<ReviewSessionDto> StartOrResumeAsync(LearningLanguage language, IReadOnlyList<string> wordGroupIds,
            CancellationToken cancellationToken = default);
    }
}
