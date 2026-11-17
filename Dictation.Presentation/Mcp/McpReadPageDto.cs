using Dictation.Core.Configuration;

namespace Dictation.Presentation.Mcp
{
    /// <summary>描述一次有界只读响应；总数在筛选后、分页前计算。</summary>
    internal sealed record McpReadPageDto<T>
    {
        public McpReadPageDto(LearningLanguage language, DateTimeOffset asOfUtc, int totalCount,
            int limit, int offset, IReadOnlyList<T> items, bool estimatedRetentionIsPrediction)
        {
            Language = language;
            AsOfUtc = asOfUtc;
            TotalCount = totalCount;
            Limit = limit;
            Offset = offset;
            Items = items;
            EstimatedRetentionIsPrediction = estimatedRetentionIsPrediction;
        }

        public LearningLanguage Language { get; }
        public DateTimeOffset AsOfUtc { get; }
        public int TotalCount { get; }
        public int Limit { get; }
        public int Offset { get; }
        public IReadOnlyList<T> Items { get; }
        public bool EstimatedRetentionIsPrediction { get; }
        public bool HasMore => (long)Offset + Items.Count < TotalCount;
    }
}
