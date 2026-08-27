using Dapper;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Lexicon
{
    internal static class LexiconSqliteQuerySupport
    {
        public static string EscapeLikePattern(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
        }

        public static async Task EnsureSchemaVersionAsync(
            SqliteConnection connection,
            string languageName,
            CancellationToken cancellationToken)
        {
            var schemaVersion = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT Value FROM BuildMetadata WHERE Key = 'schema_version' LIMIT 1;",
                cancellationToken: cancellationToken));
            if (!string.Equals(schemaVersion, "1", StringComparison.Ordinal))
            {
                var reason = new InvalidOperationException(
                    $"schema_version={schemaVersion ?? "(缺失)"}，要求为 1。");
                throw new LexiconDataAccessException(
                    $"{languageName}词库 schema_version 必须为 1。",
                    reason);
            }
        }

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
