using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>只转换成员词形的业务记忆状态与持久化模型，不计算评价或访问数据库。</summary>
    internal static class ReviewMemoryMapper
    {
        /// <summary>将调度结果与所属词形标识组合为待保存状态。</summary>
        /// <param name="wordGroupId">状态所属词组。</param>
        /// <param name="memberId">词组内独立词元的标识。</param>
        /// <param name="formCode">该成员的词形代码。</param>
        /// <param name="state">客观评价后的 FSRS 状态。</param>
        internal static ReviewFormMemoryPo ToPo(string wordGroupId, long memberId, string formCode,
            FsrsMemoryState state) => new(wordGroupId, memberId, formCode, state.Stability, state.Difficulty,
                state.LastReviewedAtUtc, state.DueAtUtc, state.ReviewCount, state.LapseCount);
    }
}
