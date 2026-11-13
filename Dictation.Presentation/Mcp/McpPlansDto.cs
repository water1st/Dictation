using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Review;

namespace Dictation.Presentation.Mcp
{
    /// <summary>返回完整计划选项和已选复习词组的一页标识；不保存或修改原计划。</summary>
    internal sealed record McpPlansDto(LearningLanguage Language, DateTimeOffset AsOfUtc,
        LearningPlanDto Learning, ReviewPlanDto Review, int TotalSelectedGroupCount, int Limit, int Offset)
    {
        public bool HasMore => (long)Offset + Review.WordGroupIds.Count < TotalSelectedGroupCount;
    }
}
