namespace Dictation.Core.Statistics
{
    /// <summary>提供双语言历史月历只读能力，不允许页面编辑进度。</summary>
    public interface ICheckInCalendarReader
    {
        /// <summary>读取该月完整日期及各语言现存完成事实。</summary>
        /// <param name="year">1–9999 年。</param>
        /// <param name="month">1–12 月。</param>
        /// <param name="cancellationToken">取消读取。</param>
        Task<CalendarMonthDto> LoadMonthAsync(int year, int month, CancellationToken cancellationToken = default);
    }
}
