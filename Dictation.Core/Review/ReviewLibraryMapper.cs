using System.Globalization;
using Dictation.DataAccess.Review;
using Dictation.DataAccess.Importing;

namespace Dictation.Core.Review
{
    /// <summary>纯转换复习持久化快照、记忆数值和展示 DTO，不执行查询或调度算法。</summary>
    internal static class ReviewLibraryMapper
    {
        /// <summary>把数据库 UTC 文本及数值还原为业务记忆状态。</summary>
        /// <param name="memory">数据库返回的记忆列。</param>
        internal static FsrsMemoryState ToState(ReviewStoredMemoryPo memory) => new(memory.Stability,
            memory.Difficulty, DateTimeOffset.Parse(memory.LastReviewedAtUtc, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(memory.DueAtUtc, CultureInfo.InvariantCulture), memory.ReviewCount, memory.LapseCount);

        /// <summary>将成员词形、已有状态及业务层已计算的风险投影为展示数据。</summary>
        /// <param name="member">完整成员快照。</param>
        /// <param name="form">该成员的一种词形。</param>
        /// <param name="state">已有状态；未评价时为空。</param>
        /// <param name="retention">业务层已计算的保持率。</param>
        /// <param name="isDue">业务层判断的到期标记。</param>
        internal static ReviewFormMemoryDto ToFormDto(ImportWordGroupMemberPo member,
            ImportWordGroupFormPo form, FsrsMemoryState? state, double? retention, bool isDue) =>
            new(member.SourceSenseId, member.Headword, form.FormCode, form.FormLabel, state is not null,
                isDue, state?.Stability, state?.Difficulty, state?.LastReviewedAtUtc, state?.DueAtUtc,
                retention, state?.ReviewCount ?? 0, state?.LapseCount ?? 0);
    }
}
