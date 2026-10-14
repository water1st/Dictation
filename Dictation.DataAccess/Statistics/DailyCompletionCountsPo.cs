namespace Dictation.DataAccess.Statistics
{
    /// <summary>表示从原始完成表查询的当天去重计数，不另行累加答题次数。</summary>
    /// <param name="LearningCompleted">完成学习的不同组数。</param>
    /// <param name="ReviewCompleted">完成复习的不同组数。</param>
    /// <param name="KanaCompleted">完成五十音的不同项目数。</param>
    public sealed record DailyCompletionCountsPo(long LearningCompleted, long ReviewCompleted, long KanaCompleted);
}
