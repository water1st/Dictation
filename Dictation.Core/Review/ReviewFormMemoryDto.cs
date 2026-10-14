namespace Dictation.Core.Review
{
    /// <summary>展示独立成员词形的预计保持率；空状态表示该形式尚未形成客观记忆。</summary>
    public sealed record ReviewFormMemoryDto(string SourceSenseId, string Headword, string FormCode,
        string FormLabel, bool HasMemory, bool IsDue, double? Stability, double? Difficulty,
        DateTimeOffset? LastReviewedAtUtc, DateTimeOffset? DueAtUtc, double? EstimatedRetention,
        int ReviewCount, int LapseCount);
}
