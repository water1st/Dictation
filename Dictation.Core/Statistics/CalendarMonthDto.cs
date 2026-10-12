namespace Dictation.Core.Statistics
{
    /// <summary>按日期排序的完整月历，不含月外填充格。</summary>
    /// <param name="Year">请求年份。</param>
    /// <param name="Month">请求月份。</param>
    /// <param name="Days">该月每天的状态，包括无记录日期。</param>
    public sealed record CalendarMonthDto(int Year, int Month, IReadOnlyList<CalendarDayDto> Days);
}
