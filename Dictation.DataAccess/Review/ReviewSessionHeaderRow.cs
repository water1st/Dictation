namespace Dictation.DataAccess.Review
{
    /// <summary>接收 Dapper 会话主表列，不承载业务或对外 API。</summary>
    internal sealed class ReviewSessionHeaderRow
    {
        public string SessionId { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string PlanJson { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int CurrentOrdinal { get; set; }
    }
}
