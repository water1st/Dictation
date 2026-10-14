using Dictation.Core.Configuration;
using Dictation.DataAccess.Statistics;

namespace Dictation.Core.Statistics
{
    /// <summary>将完成事实与业务结果转换为跨层 DTO，不查询或判定业务。</summary>
    internal static class DailyCheckInMapper
    {
        /// <summary>组合已读取的值，保留尚无快照与零目标的差别。</summary>
        /// <param name="language">当前语言。</param>
        /// <param name="date">本地日期。</param>
        /// <param name="snapshot">已有目标，未开始时为空。</param>
        /// <param name="counts">真实完成事实。</param>
        /// <param name="checkedIn">业务层达标判定。</param>
        internal static DailyCheckInDto ToDto(LearningLanguage language, DateOnly date, DailyPlanSnapshotPo? snapshot, DailyCompletionCountsPo counts, bool checkedIn) =>
            new(language, date, counts.LearningCompleted, snapshot?.LearningTarget ?? 0,
                counts.ReviewCompleted, snapshot?.ReviewTarget ?? 0, counts.KanaCompleted, snapshot?.KanaTarget ?? 0,
                snapshot is not null, checkedIn);
    }
}
