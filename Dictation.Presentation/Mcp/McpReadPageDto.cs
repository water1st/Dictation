using Dictation.Core.Configuration;

namespace Dictation.Presentation.Mcp
{
    /// <summary>描述一次有界只读响应；总数在筛选后、分页前计算。</summary>
    internal sealed record McpReadPageDto<T>(LearningLanguage Language, DateTimeOffset AsOfUtc,
        int TotalCount, int Limit, int Offset, IReadOnlyList<T> Items, bool EstimatedRetentionIsPrediction)
    {
        public bool HasMore => (long)Offset + Items.Count < TotalCount;
    }
}
