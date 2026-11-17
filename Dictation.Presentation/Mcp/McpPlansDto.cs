using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Review;

namespace Dictation.Presentation.Mcp
{
    /// <summary>返回完整计划选项和已选复习词组的一页标识；不保存或修改原计划。</summary>
    internal sealed record McpPlansDto
    {
        public McpPlansDto(LearningLanguage language, DateTimeOffset asOfUtc, LearningPlanDto learning,
            ReviewPlanDto review, int totalSelectedGroupCount, int limit, int offset)
        {
            Language = language;
            AsOfUtc = asOfUtc;
            Learning = learning;
            Review = review;
            TotalSelectedGroupCount = totalSelectedGroupCount;
            Limit = limit;
            Offset = offset;
        }

        public LearningLanguage Language { get; }
        public DateTimeOffset AsOfUtc { get; }
        public LearningPlanDto Learning { get; }
        public ReviewPlanDto Review { get; }
        public int TotalSelectedGroupCount { get; }
        public int Limit { get; }
        public int Offset { get; }
        public bool HasMore => (long)Offset + Review.WordGroupIds.Count < TotalSelectedGroupCount;
    }
}
