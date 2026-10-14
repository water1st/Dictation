using Dictation.Core.Importing;

namespace Dictation.Core.Review
{
    /// <summary>将完整词组与各词形记忆、待测和到期信息组合为只读展示结果。</summary>
    public sealed record ReviewWordGroupDto(ImportWordGroupDto WordGroup,
        IReadOnlyList<ReviewFormMemoryDto> Forms, bool NeedsInitialTest, bool IsDue,
        DateTimeOffset? NextReviewAtUtc, double? EstimatedRetention);
}
