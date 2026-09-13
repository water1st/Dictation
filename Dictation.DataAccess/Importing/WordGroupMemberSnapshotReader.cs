using System.Data.Common;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 使用调用方拥有的数据库连接读取词组成员及其释义、词形快照。
    /// </summary>
    internal sealed class WordGroupMemberSnapshotReader
    {
        /// <summary>
        /// 读取指定词组的完整成员快照，不接管连接或事务的生命周期。
        /// </summary>
        /// <param name="connection">已由调用方创建并打开的 SQLite 连接。</param>
        /// <param name="transaction">调用方当前事务；没有事务时为 <see langword="null"/>。</param>
        /// <param name="wordGroupIds">需要读取的词组标识。</param>
        /// <param name="cancellationToken">用于取消每条数据库命令的令牌。</param>
        /// <returns>以词组标识为键、成员快照为值的只读映射。</returns>
        internal async Task<IReadOnlyDictionary<string, IReadOnlyList<ImportWordGroupMemberPo>>> ReadAsync(
            SqliteConnection connection,
            DbTransaction? transaction,
            IReadOnlyList<string> wordGroupIds,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(wordGroupIds);
            cancellationToken.ThrowIfCancellationRequested();
            if (wordGroupIds.Count == 0)
            {
                return new Dictionary<string, IReadOnlyList<ImportWordGroupMemberPo>>(StringComparer.Ordinal);
            }

            var members = (await connection.QueryAsync<WordGroupMemberRow>(new CommandDefinition(
                """
                SELECT MemberId, WordGroupId, Language, SourceSenseId, SourceLexemeId, Headword, Reading,
                       PartOfSpeechLabel, AttributeLabel, IsPhrase, SortOrder,
                       MeaningLanguage, MeaningSource, MeaningPrecision, KanjiUsageCode
                FROM WordGroupMembers
                WHERE WordGroupId IN @WordGroupIds
                ORDER BY WordGroupId, SortOrder;
                """,
                new { WordGroupIds = wordGroupIds },
                transaction,
                cancellationToken: cancellationToken))).ToArray();
            if (members.Length == 0)
            {
                return new Dictionary<string, IReadOnlyList<ImportWordGroupMemberPo>>(StringComparer.Ordinal);
            }

            var memberIds = members.Select(member => member.MemberId).ToArray();
            var meanings = (await connection.QueryAsync<WordGroupMeaningRow>(new CommandDefinition(
                """
                SELECT MemberId, MeaningIndex, Text
                FROM WordGroupMeanings
                WHERE MemberId IN @MemberIds
                ORDER BY MemberId, MeaningIndex;
                """,
                new { MemberIds = memberIds },
                transaction,
                cancellationToken: cancellationToken))).ToArray();
            var forms = (await connection.QueryAsync<WordGroupFormRow>(new CommandDefinition(
                """
                SELECT MemberId, FormIndex, Surface, Reading, FormCode, ScriptCode, RoleCode,
                       FormLabel, DetailLabel, IsCommon, IsPrimary
                FROM WordGroupForms
                WHERE MemberId IN @MemberIds
                ORDER BY MemberId, FormIndex;
                """,
                new { MemberIds = memberIds },
                transaction,
                cancellationToken: cancellationToken))).ToArray();

            return WordGroupMemberSnapshotMapper.Map(members, meanings, forms);
        }
    }
}
