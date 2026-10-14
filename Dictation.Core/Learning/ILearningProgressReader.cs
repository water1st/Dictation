using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 提供按语言读取今天学习进度的能力。
    /// </summary>
    public interface ILearningProgressReader
    {
        /// <summary>
        /// 按本地日期读取词组进度，并结合当前语言的每日目标。
        /// </summary>
        /// <param name="language">需要查询的学习语言。</param>
        /// <param name="cancellationToken">取消查询的令牌。</param>
        /// <returns>今天的词组计数及当前计划目标。</returns>
        Task<LearningDailyProgressDto> GetTodayAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default);
    }
}
