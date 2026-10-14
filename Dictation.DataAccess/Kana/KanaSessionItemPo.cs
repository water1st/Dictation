namespace Dictation.DataAccess.Kana
{
    /// <summary>保存轮次项目事实快照和独立重试状态，不引用词库词条。</summary>
    public sealed class KanaSessionItemPo
    {
        public string SessionId { get; set; } = "";
        public string KanaId { get; set; } = "";
        public string Text { get; set; } = "";
        public string Reading { get; set; } = "";
        public string RowHint { get; set; } = "";
        public string Script { get; set; } = "";
        public string Range { get; set; } = "";
        public int QueueOrder { get; set; }
        public bool HasWrong { get; set; }
        public int CorrectStreak { get; set; }
        public int NextEligibleOrdinal { get; set; }
        public bool IsCompleted { get; set; }
        public string? CompletedLocalDate { get; set; }
    }
}
