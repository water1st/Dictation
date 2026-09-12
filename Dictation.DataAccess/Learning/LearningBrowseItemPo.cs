using Dictation.DataAccess.Importing;

namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示会话浏览查询返回的持久化完整词组。
    /// </summary>
    public sealed record LearningBrowseItemPo(
        int SortOrder,
        string WordGroupId,
        IReadOnlyList<ImportWordGroupMemberPo> Members);
}
