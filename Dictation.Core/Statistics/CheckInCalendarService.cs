using Dictation.DataAccess.Statistics;
using System.Globalization;

namespace Dictation.Core.Statistics
{
    /// <summary>以原历史事实和冻结目标补齐完整日期，复用每日客观资格判定。</summary>
    internal sealed class CheckInCalendarService : ICheckInCalendarReader
    {
        private readonly DailyCheckInDataAccessor data;

        /// <summary>注入月区间只读访问，不依赖现在的语言计划。</summary>
        /// <param name="data">读取历史目标与完成日期。</param>
        public CheckInCalendarService(DailyCheckInDataAccessor data) => this.data = data ?? throw new ArgumentNullException(nameof(data));

        /// <inheritdoc />
        public async Task<CalendarMonthDto> LoadMonthAsync(int year, int month, CancellationToken cancellationToken = default)
        {
            _ = new DateOnly(year, month, 1);
            cancellationToken.ThrowIfCancellationRequested();
            var rows = (await data.LoadMonthAsync(year, month, cancellationToken)).ToDictionary(row => (row.Language, row.LocalDate));
            var days = new List<CalendarDayDto>();
            for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var date = new DateOnly(year, month, day);
                var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var jp = rows.GetValueOrDefault(("Japanese", key));
                var en = rows.GetValueOrDefault(("English", key));
                days.Add(CheckInCalendarMapper.ToDay(date, jp, en,
                    DailyCheckInPolicy.IsCheckedIn(CheckInCalendarMapper.ToSnapshot(jp), CheckInCalendarMapper.ToCounts(jp)),
                    DailyCheckInPolicy.IsCheckedIn(CheckInCalendarMapper.ToSnapshot(en), CheckInCalendarMapper.ToCounts(en))));
            }
            return new(year, month, days);
        }
    }
}
