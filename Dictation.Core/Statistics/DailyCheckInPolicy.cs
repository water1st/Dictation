using Dictation.DataAccess.Statistics;

namespace Dictation.Core.Statistics
{
    /// <summary>当前打卡与历史月历共用的纯资格规则，不读取时钟、计划或数据库。</summary>
    internal static class DailyCheckInPolicy
    {
        /// <summary>有实际完成且各有效目标达到才打卡，没有目标快照不补造历史。</summary>
        /// <param name="snapshot">原冻结目标；空表示没有证据。</param>
        /// <param name="counts">现存完成事实。</param>
        internal static bool IsCheckedIn(DailyPlanSnapshotPo? snapshot, DailyCompletionCountsPo counts) =>
            snapshot is not null && (counts.LearningCompleted > 0 || counts.ReviewCompleted > 0 || counts.KanaCompleted > 0)
            && counts.LearningCompleted >= snapshot.LearningTarget
            && counts.ReviewCompleted >= snapshot.ReviewTarget
            && counts.KanaCompleted >= snapshot.KanaTarget;
    }
}
