using Dapper;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Lexicon
{
    /// <summary>
    /// 提供词库 LIKE 转义、结构版本检查和查询异常包装。
    /// </summary>
    internal static class LexiconSqliteQuerySupport
    {
        /// <summary>
        /// 转义 LIKE 模式的反斜杠、百分号和下划线，按字面检索用户输入。
        /// </summary>
        /// <param name="value">需要按字面匹配的原始文本。</param>
        /// <returns>可配合反斜杠 ESCAPE 子句使用的字面模式。</returns>
        public static string EscapeLikePattern(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
        }

        /// <summary>
        /// 读取词库元数据，拒绝缺失或不受支持的结构版本。
        /// </summary>
        /// <param name="connection">调用方已打开且负责释放的连接。</param>
        /// <param name="languageName">错误说明中使用的语言名称。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        public static async Task EnsureSchemaVersionAsync(
            SqliteConnection connection,
            string languageName,
            CancellationToken cancellationToken)
        {
            var schemaVersion = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT Value FROM BuildMetadata WHERE Key = 'schema_version' LIMIT 1;",
                cancellationToken: cancellationToken));
            if (!string.Equals(schemaVersion, "2", StringComparison.Ordinal))
            {
                var reason = new InvalidOperationException(
                    $"schema_version={schemaVersion ?? "(缺失)"}，要求为 2。");
                throw new LexiconDataAccessException(
                    $"{languageName}词库 schema_version 必须为 2。",
                    reason);
            }
        }

        /// <summary>
        /// 包装查询异常，保留语言上下文及原始原因。
        /// </summary>
        /// <param name="languageName">错误说明中使用的语言名称。</param>
        /// <param name="exception">需要保留的原始查询异常。</param>
        /// <returns>包含原始异常的词库访问异常。</returns>
        public static LexiconDataAccessException WrapQueryFailure(
            string languageName,
            Exception exception)
        {
            return new LexiconDataAccessException(
                $"读取{languageName}词库失败。",
                exception);
        }
    }
}
