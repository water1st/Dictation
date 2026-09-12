using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示呈现给学习页面的当前题目，不包含答案键。
    /// </summary>
    public sealed record LearningQuestionDto(
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
