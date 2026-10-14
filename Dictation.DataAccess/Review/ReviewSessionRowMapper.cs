using System.Globalization;

namespace Dictation.DataAccess.Review
{
    /// <summary>将会话查询列和有序组标识纯转换为持久化模型。</summary>
    internal static class ReviewSessionRowMapper
    {
        /// <summary>按数据库 UTC 文本恢复概要，保持组顺序。</summary>
        /// <param name="row">会话主表查询结果。</param>
        /// <param name="groups">同一事务读取的有序组标识。</param>
        internal static ReviewSessionPo ToPo(ReviewSessionHeaderRow row, IReadOnlyList<string> groups) =>
            new(row.SessionId, row.Language, row.PlanJson, DateTimeOffset.Parse(row.CreatedAtUtc, CultureInfo.InvariantCulture), groups);
    }
}
