namespace Dictation.DataAccess.Kana
{
    /// <summary>保存唯一活动五十音轮次的状态和待答题序。</summary>
    public sealed class KanaSessionPo
    {
        public string SessionId { get; set; } = "";
        public string StartedLocalDate { get; set; } = "";
        public string Status { get; set; } = "Active";
        public int QuestionOrdinal { get; set; }
        public string? CurrentKanaId { get; set; }
    }
}
