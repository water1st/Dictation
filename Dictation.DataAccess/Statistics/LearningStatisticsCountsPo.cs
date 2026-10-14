namespace Dictation.DataAccess.Statistics
{
    /// <summary>保存 SQL 去重后的现存已学词组总量和指定本地日完成量。</summary>
    public sealed record LearningStatisticsCountsPo(long LearnedGroupCount, long TodayLearnedGroupCount);
}
