using Dictation.DataAccess.Statistics;

namespace Dictation.Core.Statistics
{
    /// <summary>转换月历查询行与DTO，不计算资格或读取当前计划。</summary>
    internal static class CheckInCalendarMapper
    {
        /// <summary>将可空历史目标转换为原判定使用的快照。</summary>
        /// <param name="row">单日期语言查询行，没有记录时为空。</param>
        internal static DailyPlanSnapshotPo? ToSnapshot(CalendarDayCountsPo? row) => row?.LearningTarget is null ? null
            : new(row.Language, row.LocalDate, row.LearningTarget.Value, row.ReviewTarget!.Value, row.KanaTarget!.Value);

        /// <summary>保留原完成事实，缺少该日期记录时为零。</summary>
        /// <param name="row">单日期语言行。</param>
        internal static DailyCompletionCountsPo ToCounts(CalendarDayCountsPo? row) =>
            new(row?.LearningCompleted ?? 0, row?.ReviewCompleted ?? 0, row?.KanaCompleted ?? 0);

        /// <summary>将两语言事实及已判定资格组合为日期DTO。</summary>
        /// <param name="date">月内日期。</param>
        /// <param name="japanese">日语记录。</param>
        /// <param name="english">英语记录。</param>
        /// <param name="japaneseCheckedIn">日语业务资格。</param>
        /// <param name="englishCheckedIn">英语业务资格。</param>
        internal static CalendarDayDto ToDay(DateOnly date, CalendarDayCountsPo? japanese, CalendarDayCountsPo? english,
            bool japaneseCheckedIn, bool englishCheckedIn) => new(date, japaneseCheckedIn, englishCheckedIn,
                japanese?.LearningCompleted ?? 0, japanese?.ReviewCompleted ?? 0, japanese?.KanaCompleted ?? 0,
                english?.LearningCompleted ?? 0, english?.ReviewCompleted ?? 0);
    }
}
