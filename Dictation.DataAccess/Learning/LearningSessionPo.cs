namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示持久化的学习会话概要。
    /// </summary>
    public sealed record LearningSessionPo(
        string SessionId,
        string Language,
        string Status,
        int CurrentOrdinal,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        IReadOnlyList<string> WordGroupIds);
}
