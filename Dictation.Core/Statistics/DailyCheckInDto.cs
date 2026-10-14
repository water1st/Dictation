using Dictation.Core.Configuration;

namespace Dictation.Core.Statistics
{
    /// <summary>传递实际完成数、冻结目标与打卡结果；无快照不等于任务已完成。</summary>
    /// <param name="Language">学习语言。</param>
    /// <param name="LocalDate">用户本地日期。</param>
    /// <param name="LearningCompleted">已完成学习组数。</param>
    /// <param name="LearningTarget">有效学习目标。</param>
    /// <param name="ReviewCompleted">已完成复习组数。</param>
    /// <param name="ReviewTarget">有效复习目标。</param>
    /// <param name="KanaCompleted">已完成五十音项目数。</param>
    /// <param name="KanaTarget">有效五十音目标。</param>
    /// <param name="HasSnapshot">是否存在首次练习时记录的目标。</param>
    /// <param name="IsCheckedIn">是否有实际完成且各项达到目标。</param>
    public sealed record DailyCheckInDto(LearningLanguage Language, DateOnly LocalDate,
        long LearningCompleted, long LearningTarget, long ReviewCompleted, long ReviewTarget,
        long KanaCompleted, long KanaTarget, bool HasSnapshot, bool IsCheckedIn);
}
