using Dictation.Core.Configuration;

namespace Dictation.Core.Review
{
    /// <summary>提供当前语言的每日整组复习进度，不启动会话或评价记忆。</summary>
    public interface IReviewProgressReader
    {
        /// <summary>读取本地当天已完成的不同词组及当前计划目标。</summary>
        /// <param name="language">首页选定的语言。</param>
        /// <param name="cancellationToken">取消只读查询。</param>
        /// <returns>独立于学习进度的每日复习统计。</returns>
        Task<ReviewDailyProgressDto> GetTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
