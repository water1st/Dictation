using System.Globalization;

namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 将学习会话持久化行转换为会话概要。
    /// </summary>
    internal static class LearningSessionMapper
    {
        /// <summary>
        /// 将会话行及其有序词组标识转换为持久化对象。
        /// </summary>
        /// <param name="row">会话概要行。</param>
        /// <param name="wordGroupIds">按会话顺序读取的词组标识。</param>
        /// <returns>完整的学习会话概要。</returns>
        internal static LearningSessionPo Map(
            LearningSessionRow row,
            IReadOnlyList<string> wordGroupIds)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(wordGroupIds);

            return new LearningSessionPo(
                row.SessionId,
                row.Language,
                row.Status,
                row.CurrentOrdinal,
                ParseDateTime(row.CreatedAtUtc, "LearningSessions.CreatedAtUtc"),
                row.CompletedAtUtc is null
                    ? null
                    : ParseDateTime(row.CompletedAtUtc, "LearningSessions.CompletedAtUtc"),
                wordGroupIds);
        }

        private static DateTimeOffset ParseDateTime(string value, string fieldName)
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed)
                ? parsed
                : throw new LearningSessionDataAccessException(
                    $"数据库字段 {fieldName} 不是有效时间。",
                    new InvalidDataException($"数据库字段 {fieldName} 不是有效时间。"));
        }
    }
}
