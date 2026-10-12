namespace Dictation.Core.Statistics
{
    /// <summary>同一日期的日英独立徽章与完成计数，不把五十音当单词。</summary>
    /// <param name="Date">实际本地日期。</param>
    /// <param name="JapaneseIsCheckedIn">日语是否达标。</param>
    /// <param name="EnglishIsCheckedIn">英语是否达标。</param>
    /// <param name="JapaneseLearningCompleted">日语学习组数。</param>
    /// <param name="JapaneseReviewCompleted">日语复习组数。</param>
    /// <param name="JapaneseKanaCompleted">五十音项目数。</param>
    /// <param name="EnglishLearningCompleted">英语学习组数。</param>
    /// <param name="EnglishReviewCompleted">英语复习组数。</param>
    public sealed record CalendarDayDto(DateOnly Date, bool JapaneseIsCheckedIn, bool EnglishIsCheckedIn,
        long JapaneseLearningCompleted, long JapaneseReviewCompleted, long JapaneseKanaCompleted,
        long EnglishLearningCompleted, long EnglishReviewCompleted);
}
