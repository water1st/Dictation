using Dictation.Core.Learning;
using Dictation.Core.Configuration;

namespace Dictation.Core.Practice
{
    /// <summary>
    /// 表示学习或复习页面共用的客观题面，不包含答案键。
    /// </summary>
    public sealed record PracticeQuestionDto(
        long QuestionId,
        string SessionId,
        int QuestionOrdinal,
        string WordGroupId,
        LearningLanguage Language,
        LearningChannel Channel,
        LearningOutputMode OutputMode,
        string FormCode,
        string FormLabel,
        string OutputLabel,
        IReadOnlyList<string> Meanings,
        string? SpokenText);
}
