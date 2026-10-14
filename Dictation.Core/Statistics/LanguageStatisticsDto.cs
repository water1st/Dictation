using Dictation.Core.Configuration;

namespace Dictation.Core.Statistics
{
    /// <summary>提供单语言整组统计；没有客观记忆时保持率曲线为空。</summary>
    /// <param name="Language">所属语言。</param>
    /// <param name="AsOfUtc">预测使用的 UTC 快照时间。</param>
    /// <param name="LearnedGroupCount">已学过且仍存在的组数。</param>
    /// <param name="TodayLearnedGroupCount">本地当日完成学习的不同组数。</param>
    /// <param name="TodayReviewedGroupCount">本地当日完成复习的不同组数。</param>
    /// <param name="PendingTestGroupCount">尚无客观记忆的复习组数。</param>
    /// <param name="OverdueGroupCount">已有记忆且早于快照时间到期的组数。</param>
    /// <param name="TopMistakes">按错误数降序的最多十个组。</param>
    /// <param name="RetentionTrend">按组最弱已测词形计算的未来三十天平均预计保持率。</param>
    /// <param name="DueTrend">未来十四天首次到期的组数；不重复计逾期或待测组。</param>
    public sealed record LanguageStatisticsDto(LearningLanguage Language, DateTimeOffset AsOfUtc,
        long LearnedGroupCount, long TodayLearnedGroupCount, long TodayReviewedGroupCount,
        long PendingTestGroupCount, long OverdueGroupCount, IReadOnlyList<FrequentMistakeDto> TopMistakes,
        IReadOnlyList<StatisticsTrendPointDto> RetentionTrend, IReadOnlyList<StatisticsTrendPointDto> DueTrend);
}
