namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示原子创建学习会话及词形状态的持久化输入。
    /// </summary>
    public sealed record CreateLearningSessionPo(
        string SessionId,
        string Language,
        string Status,
        string StartedLocalDate,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        IReadOnlyList<LearningSessionGroupSeedPo> Groups);
}
