namespace Dictation.Presentation.Mcp
{
    /// <summary>提供累计错词摘要，不内嵌可能无限增长的原始作答历史。</summary>
    internal sealed record McpWrongAnswerDto
    {
        public McpWrongAnswerDto(string wordGroupId, string sourceSenseId, string formCode,
            string headword, string formLabel, string meanings, string placement, DateTimeOffset? dueAtUtc,
            int errorCount, DateTimeOffset? lastAnsweredAtUtc)
        {
            WordGroupId = wordGroupId;
            SourceSenseId = sourceSenseId;
            FormCode = formCode;
            Headword = headword;
            FormLabel = formLabel;
            Meanings = meanings;
            Placement = placement;
            DueAtUtc = dueAtUtc;
            ErrorCount = errorCount;
            LastAnsweredAtUtc = lastAnsweredAtUtc;
        }

        public string WordGroupId { get; }
        public string SourceSenseId { get; }
        public string FormCode { get; }
        public string Headword { get; }
        public string FormLabel { get; }
        public string Meanings { get; }
        public string Placement { get; }
        public DateTimeOffset? DueAtUtc { get; }
        public int ErrorCount { get; }
        public DateTimeOffset? LastAnsweredAtUtc { get; }
    }
}
