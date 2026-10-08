namespace Dictation.DataAccess.Review
{
    /// <summary>接收复习题目的定位和可空结果列，供纯 Mapper 转换。</summary>
    internal sealed class ReviewQuestionRow
    {
        public long QuestionId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string WordGroupId { get; set; } = string.Empty;
        public long MemberId { get; set; }
        public string FormCode { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string OutputMode { get; set; } = string.Empty;
        public int QuestionOrdinal { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? WasCorrect { get; set; }
    }
}
