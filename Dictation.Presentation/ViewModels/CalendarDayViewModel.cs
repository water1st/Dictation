using System.Globalization;
using Dictation.Core.Statistics;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>将单日业务结果适配为日历格绑定；空日期只用于周对齐。</summary>
    public sealed class CalendarDayViewModel
    {
        private readonly CalendarDayDto? day;

        /// <summary>保存不可变日期结果，不重新计算打卡资格。</summary>
        /// <param name="day">业务日期；空表示月外占位格。</param>
        public CalendarDayViewModel(CalendarDayDto? day) => this.day = day;

        public DateOnly? Date => day?.Date;
        public bool HasDate => day is not null;
        public string DayLabel => day?.Date.Day.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        public bool JapaneseIsCheckedIn => day?.JapaneseIsCheckedIn ?? false;
        public bool EnglishIsCheckedIn => day?.EnglishIsCheckedIn ?? false;
        public string? Details => day is null ? null : $"{day.Date:yyyy-MM-dd}\n日语：学习 {day.JapaneseLearningCompleted} 组，复习 {day.JapaneseReviewCompleted} 组，五十音 {day.JapaneseKanaCompleted} 个\n英语：学习 {day.EnglishLearningCompleted} 组，复习 {day.EnglishReviewCompleted} 组";
    }
}
