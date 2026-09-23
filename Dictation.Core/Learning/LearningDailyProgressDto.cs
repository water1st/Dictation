using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示当前语言今天已开始、已完成的词组数及当前每日新学目标。
    /// </summary>
    public sealed record LearningDailyProgressDto(
        LearningLanguage Language,
        long StartedGroupCount,
        long CompletedGroupCount,
        int DailyTarget);
}
