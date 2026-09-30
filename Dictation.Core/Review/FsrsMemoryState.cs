namespace Dictation.Core.Review
{
    /// <summary>保存一个成员词形的 FSRS 数值状态，不携带数据库或界面职责。</summary>
    /// <param name="Stability">记忆保持率下降到 90% 时的稳定天数。</param>
    /// <param name="Difficulty">1–10 的记忆难度。</param>
    /// <param name="LastReviewedAtUtc">上次客观评价时间。</param>
    /// <param name="DueAtUtc">下次到期时间。</param>
    /// <param name="ReviewCount">累计评价次数。</param>
    /// <param name="LapseCount">累计 Again 次数。</param>
    internal sealed record FsrsMemoryState(
        double Stability,
        double Difficulty,
        DateTimeOffset LastReviewedAtUtc,
        DateTimeOffset DueAtUtc,
        int ReviewCount,
        int LapseCount);
}
