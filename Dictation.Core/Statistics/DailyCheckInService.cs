using System.Globalization;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.DataAccess.Statistics;

namespace Dictation.Core.Statistics
{
    /// <summary>按冻结目标和现存完成事实判定打卡，不补造历史或修改记忆。</summary>
    internal sealed class DailyCheckInService : IDailyCheckInReader
    {
        private readonly DailyCheckInDataAccessor data;
        private readonly TimeProvider clock;

        /// <summary>组合事实查询和统一时钟。</summary>
        /// <param name="data">用户数据库统计访问。</param>
        /// <param name="clock">提供实际本地日期。</param>
        public DailyCheckInService(DailyCheckInDataAccessor data, TimeProvider clock)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <inheritdoc />
        public async Task<DailyCheckInDto> GetTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            PracticePlanOptionValidator.ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var date = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var snapshot = await data.GetSnapshotAsync(language.ToString(), key, cancellationToken);
            var counts = await data.GetCompletionCountsAsync(language.ToString(), key, cancellationToken);
            return DailyCheckInMapper.ToDto(language, date, snapshot, counts, DailyCheckInPolicy.IsCheckedIn(snapshot, counts));
        }
    }
}
