using Dictation.Core.Configuration;
using Dictation.Core.Mistakes;
using Dictation.Core.Review;
using Dictation.DataAccess.Statistics;
using System.Globalization;

namespace Dictation.Core.Statistics
{
    /// <summary>组合完成事实、原错题和复习快照，复用 FSRS 预测而不改变学习状态。</summary>
    internal sealed class LanguageStatisticsService : ILanguageStatisticsReader
    {
        private readonly LearningStatisticsDataAccessor learning;
        private readonly DailyCheckInDataAccessor daily;
        private readonly IReviewLibraryQuery library;
        private readonly IWrongAnswerQuery errors;
        private readonly FsrsScheduler scheduler;
        private readonly TimeProvider clock;

        /// <summary>组合窄查询能力和纯记忆算法，不依赖 UI 或答题服务。</summary>
        /// <param name="learning">读取整组学习完成数。</param>
        /// <param name="daily">读取当天复习完成数。</param>
        /// <param name="library">加载一次语言隔离的复习快照。</param>
        /// <param name="errors">读取原累计错题。</param>
        /// <param name="scheduler">复用既有 FSRS 算法。</param>
        /// <param name="clock">提供统一时间与本地时区。</param>
        public LanguageStatisticsService(LearningStatisticsDataAccessor learning, DailyCheckInDataAccessor daily,
            IReviewLibraryQuery library, IWrongAnswerQuery errors, FsrsScheduler scheduler, TimeProvider clock)
        {
            this.learning = learning ?? throw new ArgumentNullException(nameof(learning));
            this.daily = daily ?? throw new ArgumentNullException(nameof(daily));
            this.library = library ?? throw new ArgumentNullException(nameof(library));
            this.errors = errors ?? throw new ArgumentNullException(nameof(errors));
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <inheritdoc />
        public async Task<LanguageStatisticsDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English)) throw new ArgumentOutOfRangeException(nameof(language));
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await library.LoadAsync(language, cancellationToken);
            if (snapshot.Language != language) throw new InvalidDataException("复习快照语言与统计请求不符。");
            var now = snapshot.AsOfUtc.ToUniversalTime();
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, clock.LocalTimeZone).DateTime);
            var dateText = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var counts = await learning.LoadAsync(language.ToString(), dateText, cancellationToken);
            var today = await daily.GetCompletionCountsAsync(language.ToString(), dateText, cancellationToken);
            var mistakes = await errors.LoadAsync(language, cancellationToken);

            // 每组只使用已有客观状态；普通形、过去式等保持独立记忆，但报表计数仍是一个词组。
            var assessed = snapshot.Groups.Select(group => group.Forms.Where(form => form.HasMemory)
                .Select(LanguageStatisticsMapper.ToState).ToArray()).ToArray();
            foreach (var states in assessed)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var state in states) _ = scheduler.GetRetrievability(state, now);
            }
            var knownGroups = assessed.Where(states => states.Length > 0).ToArray();
            var earliestDue = knownGroups.Select(states => states.Min(state => state.DueAtUtc)).ToArray();
            var retention = new List<StatisticsTrendPointDto>();
            var due = new List<StatisticsTrendPointDto>();
            var remainingDays = DateOnly.MaxValue.DayNumber - localDate.DayNumber + 1;
            for (var offset = 0; offset < Math.Min(30, remainingDays); offset++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var date = localDate.AddDays(offset);
                // 固定假设不再复习，按最弱已测形式取组值，再让各组等权平均。
                if (knownGroups.Length > 0)
                {
                    var forecast = now.AddDays(Math.Min(offset, Math.Floor((DateTimeOffset.MaxValue - now).TotalDays)));
                    retention.Add(new(date, knownGroups.Average(states => states.Min(state => scheduler.GetRetrievability(state, forecast)))));
                }
                if (offset < 14)
                {
                    due.Add(new(date, earliestDue.Count(time => time >= now &&
                        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, clock.LocalTimeZone).DateTime) == date)));
                }
            }
            return new(language, now, counts.LearnedGroupCount, counts.TodayLearnedGroupCount, today.ReviewCompleted,
                assessed.LongCount(states => states.Length == 0), earliestDue.LongCount(time => time < now),
                LanguageStatisticsMapper.ToTopMistakes(mistakes), retention, due);
        }
    }
}
