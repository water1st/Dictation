using Dictation.Core.Importing;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示会话浏览列表中的完整词组。
    /// </summary>
    public sealed record LearningBrowseItemDto(
        int SortOrder,
        string WordGroupId,
        IReadOnlyList<ImportWordGroupMemberDto> Members);
}
