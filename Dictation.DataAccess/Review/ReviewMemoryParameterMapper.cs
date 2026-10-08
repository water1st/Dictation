using System.Globalization;

namespace Dictation.DataAccess.Review
{
    /// <summary>将记忆 PO 的 UTC 时间纯转换为 SQLite 参数，不执行查询或评价。</summary>
    internal static class ReviewMemoryParameterMapper
    {
        /// <summary>产生 Dapper 所需的稳定列名参数及 UTC 文本。</summary>
        /// <param name="memory">业务层已计算、待持久化的记忆状态。</param>
        internal static object ToParameters(ReviewFormMemoryPo memory) => new
        {
            memory.WordGroupId, memory.MemberId, memory.FormCode, memory.Stability, memory.Difficulty,
            LastReviewedAtUtc = memory.LastReviewedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DueAtUtc = memory.DueAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), memory.ReviewCount, memory.LapseCount
        };
    }
}
