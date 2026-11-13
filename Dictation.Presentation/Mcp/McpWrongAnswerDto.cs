namespace Dictation.Presentation.Mcp
{
    /// <summary>提供累计错词摘要，不内嵌可能无限增长的原始作答历史。</summary>
    internal sealed record McpWrongAnswerDto(string WordGroupId, string SourceSenseId, string FormCode,
        string Headword, string FormLabel, string Meanings, string Placement, DateTimeOffset? DueAtUtc,
        int ErrorCount, DateTimeOffset? LastAnsweredAtUtc);
}
