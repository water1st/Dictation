namespace Dictation.DataAccess.Kana
{
    /// <summary>保存与词汇记忆完全独立的五十音FSRS数值列。</summary>
    public sealed class KanaMemoryPo
    {
        public string KanaId { get; set; } = "";
        public double Stability { get; set; }
        public double Difficulty { get; set; }
        public string LastReviewedAtUtc { get; set; } = "";
        public string DueAtUtc { get; set; } = "";
        public int ReviewCount { get; set; }
        public int LapseCount { get; set; }
    }
}
