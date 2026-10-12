using Dictation.Core.Configuration;

namespace Dictation.Core.Statistics
{
    /// <summary>提供只读每日打卡能力，不允许页面直接查询数据库或推进进度。</summary>
    public interface IDailyCheckInReader
    {
        /// <summary>按本地今天读取指定语言的有效目标与客观完成情况。</summary>
        /// <param name="language">首页选择的学习语言。</param>
        /// <param name="cancellationToken">取消查询。</param>
        Task<DailyCheckInDto> GetTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
