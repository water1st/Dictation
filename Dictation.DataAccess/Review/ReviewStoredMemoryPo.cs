namespace Dictation.DataAccess.Review
{
    /// <summary>表示复习记忆表联接来源义项后的原始查询列，时间保留数据库 UTC 文本。</summary>
    public sealed class ReviewStoredMemoryPo
    {
        public string WordGroupId { get; set; } = string.Empty;
        public long MemberId { get; set; }
        public string SourceSenseId { get; set; } = string.Empty;
        public string FormCode { get; set; } = string.Empty;
        public double Stability { get; set; }
        public double Difficulty { get; set; }
        public string LastReviewedAtUtc { get; set; } = string.Empty;
        public string DueAtUtc { get; set; } = string.Empty;
        public int ReviewCount { get; set; }
        public int LapseCount { get; set; }
    }
}
