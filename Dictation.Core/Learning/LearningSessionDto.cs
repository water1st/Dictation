using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示一轮学习会话的进度与词组范围。
    /// </summary>
    public sealed record LearningSessionDto(
        string SessionId,
        LearningLanguage Language,
        LearningSessionStatus Status,
        int CurrentOrdinal,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        IReadOnlyList<string> WordGroupIds);
}
