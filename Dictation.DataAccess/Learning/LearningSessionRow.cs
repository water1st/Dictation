namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示学习会话概要查询返回的持久化行。
    /// </summary>
    internal sealed class LearningSessionRow
    {
        public string SessionId { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int CurrentOrdinal { get; set; }

        public string CreatedAtUtc { get; set; } = string.Empty;

        public string? CompletedAtUtc { get; set; }
    }
}
