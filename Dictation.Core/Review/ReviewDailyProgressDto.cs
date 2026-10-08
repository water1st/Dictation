using Dictation.Core.Configuration;

namespace Dictation.Core.Review
{
    /// <summary>表示当前语言今天完成的不同整组数量和当前每日复习目标。</summary>
    public sealed record ReviewDailyProgressDto(LearningLanguage Language, int CompletedGroupCount, int DailyTarget);
}
