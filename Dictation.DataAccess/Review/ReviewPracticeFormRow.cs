namespace Dictation.DataAccess.Review
{
    /// <summary>接收复习本轮成绩与成员元数据联表查询，不承载业务规则。</summary>
    internal sealed class ReviewPracticeFormRow
    {
        public string WordGroupId { get; set; } = string.Empty;
        public long MemberId { get; set; }
        public string FormCode { get; set; } = string.Empty;
        public string FormLabel { get; set; } = string.Empty;
        public int IsPhrase { get; set; }
        public string? KanjiUsageCode { get; set; }
        public string MeaningLanguage { get; set; } = string.Empty;
        public int CorrectStreak { get; set; }
        public int IsMastered { get; set; }
        public int NextEligibleOrdinal { get; set; }
        public int MeaningWritingCount { get; set; }
        public int DictationCount { get; set; }
        public int LatinCount { get; set; }
        public int KanaCount { get; set; }
        public int KanjiCount { get; set; }
        public int CorrectCount { get; set; }
        public int ErrorCount { get; set; }
    }
}
