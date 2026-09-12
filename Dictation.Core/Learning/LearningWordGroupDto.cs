using Dictation.Core.Configuration;
using Dictation.Core.Importing;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 表示学习库中的完整词组及其全部成员。
    /// </summary>
    public sealed record LearningWordGroupDto(
        string WordGroupId,
        LearningLanguage Language,
        string AnchorSourceSenseId,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<ImportWordGroupMemberDto> Members);
}
