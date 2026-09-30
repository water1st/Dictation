using System.Globalization;
using Dictation.DataAccess.Importing;

namespace Dictation.DataAccess.Review
{
    /// <summary>将数据库列与共享成员快照纯转换为复习词组持久化结果。</summary>
    internal static class ReviewLibrarySnapshotMapper
    {
        /// <summary>保持数据库词组次序并保留全部成员，不计算复习风险。</summary>
        /// <param name="groups">当前查询的原始词组列。</param>
        /// <param name="members">以词组标识为键的完整成员快照。</param>
        internal static IReadOnlyList<ImportWordGroupPo> ToGroups(IReadOnlyList<ReviewLibraryGroupRow> groups,
            IReadOnlyDictionary<string, IReadOnlyList<ImportWordGroupMemberPo>> members) => groups.Select(group =>
                new ImportWordGroupPo(group.WordGroupId, group.Language, group.AnchorSourceSenseId,
                    group.Placement, DateTimeOffset.Parse(group.CreatedAtUtc, CultureInfo.InvariantCulture),
                    members.TryGetValue(group.WordGroupId, out var values) ? values : Array.Empty<ImportWordGroupMemberPo>())).ToArray();
    }
}
