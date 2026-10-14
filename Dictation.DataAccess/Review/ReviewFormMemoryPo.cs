namespace Dictation.DataAccess.Review
{
    /// <summary>表示用户数据库中一个成员词形的客观记忆状态，不执行调度计算。</summary>
    public sealed record ReviewFormMemoryPo(
        string WordGroupId,
        long MemberId,
        string FormCode,
        double Stability,
        double Difficulty,
        DateTimeOffset LastReviewedAtUtc,
        DateTimeOffset DueAtUtc,
        int ReviewCount,
        int LapseCount);
}
