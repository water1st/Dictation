using Dictation.Core.Review;

namespace Dictation.Presentation.Mcp
{
    /// <summary>保留用户词组标识与原始成员词形记忆，不将组级保持率冒充词形记忆。</summary>
    internal sealed record McpMemoryDetailDto(string WordGroupId, ReviewFormMemoryDto Memory);
}
