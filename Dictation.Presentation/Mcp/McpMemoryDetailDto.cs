using Dictation.Core.Review;

namespace Dictation.Presentation.Mcp
{
    /// <summary>保留用户词组标识与原始成员词形记忆，不将组级保持率冒充词形记忆。</summary>
    internal sealed record McpMemoryDetailDto
    {
        public McpMemoryDetailDto(string wordGroupId, ReviewFormMemoryDto memory)
        {
            WordGroupId = wordGroupId;
            Memory = memory;
        }

        public string WordGroupId { get; }
        public ReviewFormMemoryDto Memory { get; }
    }
}
