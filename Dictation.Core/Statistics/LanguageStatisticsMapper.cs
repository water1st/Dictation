using Dictation.Core.Mistakes;
using Dictation.Core.Review;

namespace Dictation.Core.Statistics
{
    /// <summary>纯转换已有记忆字段及累计错误，不读取数据库或改变状态。</summary>
    internal static class LanguageStatisticsMapper
    {
        /// <summary>还原已测词形的算法输入；不为未测形式伪造默认记忆。</summary>
        /// <param name="form">复习快照中的已测词形。</param>
        /// <returns>交由既有 FSRS 校验和计算的数值状态。</returns>
        internal static FsrsMemoryState ToState(ReviewFormMemoryDto form) => new(
            form.Stability ?? throw new InvalidDataException("已测词形缺少稳定度。"),
            form.Difficulty ?? throw new InvalidDataException("已测词形缺少难度。"),
            form.LastReviewedAtUtc ?? throw new InvalidDataException("已测词形缺少评价时间。"),
            form.DueAtUtc ?? throw new InvalidDataException("已测词形缺少到期时间。"), form.ReviewCount, form.LapseCount);

        /// <summary>汇总原查询的独立错误观察，同组多词形不膨胀词组数量。</summary>
        /// <param name="errors">既有错题服务返回的真实目标观察；该查询已避免主问题与共享目标重复读取。</param>
        /// <returns>最多十个组，错误数降序，组 ID 稳定排序。</returns>
        internal static IReadOnlyList<FrequentMistakeDto> ToTopMistakes(IReadOnlyList<WrongAnswerItemDto> errors) => errors
            .GroupBy(item => item.WordGroupId, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.OrderBy(item => item.SourceSenseId, StringComparer.Ordinal).ThenBy(item => item.FormCode, StringComparer.Ordinal).First();
                return new FrequentMistakeDto(group.Key, first.Headword, first.Meanings, group.Sum(item => (long)item.Attempts.Count));
            })
            .Where(item => item.ErrorCount > 0).OrderByDescending(item => item.ErrorCount)
            .ThenBy(item => item.WordGroupId, StringComparer.Ordinal).Take(10).ToArray();
    }
}
