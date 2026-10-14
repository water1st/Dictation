using Dictation.Core.Configuration;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 表示由锚定义项确定的完整词组业务快照。
    /// </summary>
    public sealed class ImportWordGroupDto
    {
        /// <summary>
        /// 初始化完整词组业务快照。
        /// </summary>
        /// <param name="wordGroupId">词组稳定标识。</param>
        /// <param name="language">词组学习语言。</param>
        /// <param name="anchorSourceSenseId">锚定义项标识。</param>
        /// <param name="placement">词组当前所属业务区域。</param>
        /// <param name="createdAtUtc">词组创建时间。</param>
        /// <param name="members">按词库权威顺序保存的全部成员。</param>
        public ImportWordGroupDto(
            string wordGroupId,
            LearningLanguage language,
            string anchorSourceSenseId,
            ImportWordGroupPlacement placement,
            DateTimeOffset createdAtUtc,
            IReadOnlyList<ImportWordGroupMemberDto> members)
        {
            WordGroupId = wordGroupId;
            Language = language;
            AnchorSourceSenseId = anchorSourceSenseId;
            Placement = placement;
            CreatedAtUtc = createdAtUtc;
            Members = members ?? throw new ArgumentNullException(nameof(members));
        }

        public string WordGroupId { get; }

        public LearningLanguage Language { get; }

        public string AnchorSourceSenseId { get; }

        public ImportWordGroupPlacement Placement { get; }

        public DateTimeOffset CreatedAtUtc { get; }

        public IReadOnlyList<ImportWordGroupMemberDto> Members { get; }
    }
}
