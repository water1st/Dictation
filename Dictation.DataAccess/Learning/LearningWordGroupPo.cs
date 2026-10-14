using Dictation.DataAccess.Importing;

namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示学习库中持久化的完整词组。
    /// </summary>
    public sealed record LearningWordGroupPo(
        string WordGroupId,
        string Language,
        string AnchorSourceSenseId,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<ImportWordGroupMemberPo> Members);
}
