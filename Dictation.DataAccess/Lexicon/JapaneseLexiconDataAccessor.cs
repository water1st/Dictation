using Dapper;

namespace Dictation.DataAccess.Lexicon
{
    /// <summary>
    /// 通过只读 SQL 查询日语条目、释义和词形快照。
    /// </summary>
    public sealed class JapaneseLexiconDataAccessor
    {
        private readonly LexiconSqliteConnectionFactory connectionFactory;

        /// <summary>
        /// 通过只读 SQL 查询日语条目、释义和词形快照。
        /// </summary>
        /// <param name="connectionFactory">创建本层所需 SQLite 连接的工厂。</param>
        public JapaneseLexiconDataAccessor(LexiconSqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory
                ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        /// <summary>
        /// 按字面前缀检索词形，优先精确匹配并按 SQL 顺序读取完整快照。
        /// </summary>
        /// <param name="normalizedQuery">已由业务层规范化的非空检索文本。</param>
        /// <param name="limit">最多返回的记录数，词库检索范围为 1–50。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        /// <param name="offset">跳过的有序条目数量。</param>
        /// <param name="exactOnly">仅检索精确词形，不混入前缀建议。</param>
        /// <returns>按查询优先级排列的快照；无匹配时为空集合。</returns>
        public async Task<IReadOnlyList<JapaneseLexiconLookupPo>> SearchAsync(
            string normalizedQuery,
            int limit,
            CancellationToken cancellationToken = default,
            int offset = 0,
            bool exactOnly = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedQuery);
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 50);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateJapaneseConnection();
                await connection.OpenAsync(cancellationToken);
                await LexiconSqliteQuerySupport.EnsureSchemaVersionAsync(
                    connection,
                    "日语",
                    cancellationToken);

                var escapedQuery = LexiconSqliteQuerySupport.EscapeLikePattern(normalizedQuery);
                // 谓词仅由内部模式选择；用户输入始终使用 SQL 参数。
                var matchCondition = exactOnly
                    ? "Forms.Surface = @Query"
                    : "Forms.Surface = @Query OR Forms.Surface LIKE @Prefix ESCAPE '\\'";
                var entryIds = (await connection.QueryAsync<string>(new CommandDefinition(
                    $"""
                    SELECT Forms.EntryId
                    FROM Forms
                    INNER JOIN Entries ON Entries.EntryId = Forms.EntryId
                    WHERE {matchCondition}
                    GROUP BY Forms.EntryId, Entries.SourceSequence, Entries.SenseIndex
                    ORDER BY MIN(CASE WHEN Forms.Surface = @Query THEN 0 ELSE 1 END),
                             Entries.SourceSequence, Entries.SenseIndex, Forms.EntryId
                    LIMIT @Limit OFFSET @Offset;
                    """,
                    new
                    {
                        Query = normalizedQuery,
                        Prefix = escapedQuery + "%",
                        Limit = limit,
                        Offset = offset
                    },
                    cancellationToken: cancellationToken))).ToArray();
                if (entryIds.Length == 0)
                {
                    return Array.Empty<JapaneseLexiconLookupPo>();
                }

                return await LoadEntriesAsync(connection, entryIds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LexiconDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw LexiconSqliteQuerySupport.WrapQueryFailure("日语", exception);
            }
        }

        /// <summary>
        /// 按稳定条目标识读取完整日语词库快照。
        /// </summary>
        /// <param name="entryId">稳定日语条目标识。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        /// <returns>条目快照；不存在时为空。</returns>
        public async Task<JapaneseLexiconLookupPo?> GetByEntryIdAsync(
            string entryId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateJapaneseConnection();
                await connection.OpenAsync(cancellationToken);
                await LexiconSqliteQuerySupport.EnsureSchemaVersionAsync(
                    connection,
                    "日语",
                    cancellationToken);
                var exists = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                    "SELECT 1 FROM Entries WHERE EntryId = @EntryId LIMIT 1;",
                    new { EntryId = entryId },
                    cancellationToken: cancellationToken));
                if (exists is null)
                {
                    return null;
                }

                return (await LoadEntriesAsync(
                    connection,
                    new[] { entryId },
                    cancellationToken)).Single();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LexiconDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw LexiconSqliteQuerySupport.WrapQueryFailure("日语", exception);
            }
        }

        private static async Task<IReadOnlyList<JapaneseLexiconLookupPo>> LoadEntriesAsync(
            Microsoft.Data.Sqlite.SqliteConnection connection,
            IReadOnlyList<string> entryIds,
            CancellationToken cancellationToken)
        {
            var parameters = new { EntryIds = entryIds };
            var entries = (await connection.QueryAsync<EntryRow>(new CommandDefinition(
                """
                SELECT EntryId, PrimaryWritten, PrimaryReading, LexicalCategory,
                       VerbClass, Transitivity, KanjiUsage, IsPhrase
                FROM Entries
                WHERE EntryId IN @EntryIds;
                """,
                parameters,
                cancellationToken: cancellationToken))).ToDictionary(
                row => row.EntryId,
                StringComparer.Ordinal);
            var partsOfSpeech = (await connection.QueryAsync<PartOfSpeechRow>(new CommandDefinition(
                """
                SELECT EntryId, SortIndex, Value
                FROM PartsOfSpeech
                WHERE EntryId IN @EntryIds
                ORDER BY EntryId, SortIndex;
                """,
                parameters,
                cancellationToken: cancellationToken)))
                .GroupBy(row => row.EntryId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group.Select(row => row.Value).ToArray(),
                    StringComparer.Ordinal);
            var meanings = (await connection.QueryAsync<MeaningRow>(new CommandDefinition(
                """
                SELECT EntryId, MeaningIndex, Text, MeaningSource, MeaningPrecision
                FROM ChineseMeanings
                WHERE EntryId IN @EntryIds
                ORDER BY EntryId, MeaningIndex;
                """,
                parameters,
                cancellationToken: cancellationToken)))
                .GroupBy(row => row.EntryId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<MeaningRow>)group.ToArray(),
                    StringComparer.Ordinal);
            var forms = (await connection.QueryAsync<FormRow>(new CommandDefinition(
                """
                SELECT EntryId, Surface, Reading, FormKind, ScriptKind, LexicalRole, IsCommon, IsPrimary
                FROM Forms
                WHERE EntryId IN @EntryIds
                ORDER BY EntryId, FormId;
                """,
                parameters,
                cancellationToken: cancellationToken)))
                .GroupBy(row => row.EntryId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<JapaneseLexiconLookupFormPo>)group
                        .Select(row => new JapaneseLexiconLookupFormPo(
                            row.Surface,
                            row.Reading,
                            row.FormKind,
                            row.ScriptKind,
                            row.LexicalRole,
                            row.IsCommon != 0,
                            row.IsPrimary != 0))
                        .ToArray(),
                    StringComparer.Ordinal);

            return entryIds
                .Where(entries.ContainsKey)
                .Select(entryId =>
                {
                    var entry = entries[entryId];
                    var entryMeaningRows = meanings.TryGetValue(entryId, out var foundMeanings)
                        ? foundMeanings
                        : Array.Empty<MeaningRow>();
                    return new JapaneseLexiconLookupPo(
                        entry.EntryId,
                        entry.PrimaryWritten,
                        entry.PrimaryReading,
                        entry.LexicalCategory,
                        entry.VerbClass,
                        entry.Transitivity,
                        entry.KanjiUsage,
                        entry.IsPhrase != 0,
                        partsOfSpeech.TryGetValue(entryId, out var entryPartsOfSpeech)
                            ? entryPartsOfSpeech
                            : Array.Empty<string>(),
                        entryMeaningRows.FirstOrDefault()?.MeaningSource ?? "Missing",
                        entryMeaningRows.FirstOrDefault()?.MeaningPrecision ?? "Missing",
                        entryMeaningRows.Select(row => row.Text).ToArray(),
                        forms.TryGetValue(entryId, out var entryForms)
                            ? entryForms
                            : Array.Empty<JapaneseLexiconLookupFormPo>());
                })
                .ToArray();
        }

        private sealed class EntryRow
        {
            public string EntryId { get; set; } = string.Empty;

            public string PrimaryWritten { get; set; } = string.Empty;

            public string PrimaryReading { get; set; } = string.Empty;

            public string LexicalCategory { get; set; } = string.Empty;

            public string VerbClass { get; set; } = string.Empty;

            public string Transitivity { get; set; } = string.Empty;

            public string KanjiUsage { get; set; } = string.Empty;

            public int IsPhrase { get; set; }
        }

        private sealed class PartOfSpeechRow
        {
            public string EntryId { get; set; } = string.Empty;

            public int SortIndex { get; set; }

            public string Value { get; set; } = string.Empty;
        }

        private sealed class MeaningRow
        {
            public string EntryId { get; set; } = string.Empty;

            public int MeaningIndex { get; set; }

            public string Text { get; set; } = string.Empty;

            public string MeaningSource { get; set; } = string.Empty;

            public string MeaningPrecision { get; set; } = string.Empty;
        }

        private sealed class FormRow
        {
            public string EntryId { get; set; } = string.Empty;

            public long FormId { get; set; }

            public string Surface { get; set; } = string.Empty;

            public string Reading { get; set; } = string.Empty;

            public string FormKind { get; set; } = string.Empty;

            public string ScriptKind { get; set; } = string.Empty;

            public string LexicalRole { get; set; } = string.Empty;

            public int IsCommon { get; set; }

            public int IsPrimary { get; set; }
        }
    }
}
