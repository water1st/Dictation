using Dapper;

namespace Dictation.DataAccess.Lexicon
{
    /// <summary>
    /// 通过只读 SQL 查询英语学习义项、派生关联与词形快照。
    /// </summary>
    public sealed class EnglishLexiconDataAccessor
    {
        private readonly LexiconSqliteConnectionFactory connectionFactory;

        /// <summary>
        /// 通过只读 SQL 查询英语学习义项、派生关联与词形快照。
        /// </summary>
        /// <param name="connectionFactory">创建本层所需 SQLite 连接的工厂。</param>
        public EnglishLexiconDataAccessor(LexiconSqliteConnectionFactory connectionFactory)
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
        /// <param name="offset">跳过的有序义项数量。</param>
        /// <param name="exactOnly">仅检索精确词形，不混入前缀建议；英语字母忽略大小写。</param>
        /// <returns>按查询优先级排列的快照；无匹配时为空集合。</returns>
        public async Task<IReadOnlyList<EnglishLexiconLookupPo>> SearchAsync(
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
                await using var connection = connectionFactory.CreateEnglishConnection();
                await connection.OpenAsync(cancellationToken);
                await LexiconSqliteQuerySupport.EnsureSchemaVersionAsync(
                    connection,
                    "英语",
                    cancellationToken);

                var escapedQuery = LexiconSqliteQuerySupport.EscapeLikePattern(normalizedQuery);
                // 谓词仅由内部模式选择；大小写策略与英语录入规范一致。
                var matchCondition = exactOnly
                    ? "Forms.Surface = @Query COLLATE NOCASE"
                    : "Forms.Surface = @Query COLLATE NOCASE OR Forms.Surface LIKE @Prefix ESCAPE '\\'";
                var learningSenseIds = (await connection.QueryAsync<string>(new CommandDefinition(
                    $"""
                    SELECT LearningSenses.LearningSenseId
                    FROM Forms
                    INNER JOIN Senses ON Senses.LexemeId = Forms.LexemeId
                    INNER JOIN LearningSenseSources ON LearningSenseSources.SenseId = Senses.SenseId
                    INNER JOIN LearningSenses ON LearningSenses.LearningSenseId = LearningSenseSources.LearningSenseId
                    WHERE {matchCondition}
                    GROUP BY LearningSenses.LearningSenseId, LearningSenses.SortOrder
                    ORDER BY MIN(CASE WHEN Forms.Surface = @Query COLLATE NOCASE THEN 0 ELSE 1 END),
                             LearningSenses.SortOrder, LearningSenses.LearningSenseId
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
                return await LoadLearningSensesAsync(connection, learningSenseIds, cancellationToken);
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
                throw LexiconSqliteQuerySupport.WrapQueryFailure("英语", exception);
            }
        }

        /// <summary>
        /// 读取锚点及直接派生关联义项；短语只返回锚点自身。
        /// </summary>
        /// <param name="learningSenseId">作为派生组锚点的稳定学习义项标识。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        /// <returns>锚点优先的义项快照；锚点不存在时为空集合。</returns>
        public async Task<IReadOnlyList<EnglishLexiconLookupPo>> GetSenseGroupAsync(
            string learningSenseId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(learningSenseId);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateEnglishConnection();
                await connection.OpenAsync(cancellationToken);
                await LexiconSqliteQuerySupport.EnsureSchemaVersionAsync(
                    connection,
                    "英语",
                    cancellationToken);

                var anchor = await connection.QuerySingleOrDefaultAsync<LearningSenseRow>(new CommandDefinition(
                    """
                    SELECT LearningSenseId, SourceLexemeId, Headword, PartOfSpeech, IsPhrase,
                           MeaningSource, MeaningPrecision, SortOrder
                    FROM LearningSenses
                    WHERE LearningSenseId = @LearningSenseId;
                    """,
                    new { LearningSenseId = learningSenseId },
                    cancellationToken: cancellationToken));
                if (anchor is null)
                {
                    return Array.Empty<EnglishLexiconLookupPo>();
                }

                var groupIds = new List<string> { learningSenseId };
                if (anchor.IsPhrase == 0)
                {
                    var neighborIds = (await connection.QueryAsync<string>(new CommandDefinition(
                        """
                        WITH AnchorSources AS (
                            SELECT SenseId
                            FROM LearningSenseSources
                            WHERE LearningSenseId = @LearningSenseId
                        ), NeighborSources AS (
                            SELECT CASE
                                WHEN Derivations.SourceSenseId = AnchorSources.SenseId THEN Derivations.TargetSenseId
                                ELSE Derivations.SourceSenseId
                            END AS SenseId
                            FROM AnchorSources
                            INNER JOIN Derivations
                                ON Derivations.SourceSenseId = AnchorSources.SenseId
                                OR Derivations.TargetSenseId = AnchorSources.SenseId
                        )
                        SELECT LearningSenses.LearningSenseId
                        FROM NeighborSources
                        INNER JOIN LearningSenseSources
                            ON LearningSenseSources.SenseId = NeighborSources.SenseId
                        INNER JOIN LearningSenses
                            ON LearningSenses.LearningSenseId = LearningSenseSources.LearningSenseId
                        WHERE LearningSenses.IsPhrase = 0
                        GROUP BY LearningSenses.LearningSenseId, LearningSenses.SortOrder
                        ORDER BY LearningSenses.SortOrder, LearningSenses.LearningSenseId;
                        """,
                        new { LearningSenseId = learningSenseId },
                        cancellationToken: cancellationToken))).ToArray();
                    foreach (var neighborId in neighborIds)
                    {
                        if (!groupIds.Contains(neighborId, StringComparer.Ordinal))
                        {
                            groupIds.Add(neighborId);
                        }
                    }
                }

                return await LoadLearningSensesAsync(connection, groupIds, cancellationToken);
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
                throw LexiconSqliteQuerySupport.WrapQueryFailure("英语", exception);
            }
        }

        private static async Task<IReadOnlyList<EnglishLexiconLookupPo>> LoadLearningSensesAsync(
            Microsoft.Data.Sqlite.SqliteConnection connection,
            IReadOnlyList<string> orderedLearningSenseIds,
            CancellationToken cancellationToken)
        {
            if (orderedLearningSenseIds.Count == 0)
            {
                return Array.Empty<EnglishLexiconLookupPo>();
            }

            var parameters = new { LearningSenseIds = orderedLearningSenseIds };
            var learningSenses = (await connection.QueryAsync<LearningSenseRow>(new CommandDefinition(
                """
                SELECT LearningSenseId, SourceLexemeId, Headword, PartOfSpeech, IsPhrase,
                       MeaningSource, MeaningPrecision, SortOrder
                FROM LearningSenses
                WHERE LearningSenseId IN @LearningSenseIds;
                """,
                parameters,
                cancellationToken: cancellationToken))).ToDictionary(
                row => row.LearningSenseId,
                StringComparer.Ordinal);
            var meanings = (await connection.QueryAsync<MeaningRow>(new CommandDefinition(
                """
                SELECT LearningSenseId, MeaningIndex, Text
                FROM LearningMeanings
                WHERE LearningSenseId IN @LearningSenseIds
                ORDER BY LearningSenseId, MeaningIndex;
                """,
                parameters,
                cancellationToken: cancellationToken)))
                .GroupBy(row => row.LearningSenseId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group.Select(row => row.Text).ToArray(),
                    StringComparer.Ordinal);
            var forms = (await connection.QueryAsync<LearningFormRow>(new CommandDefinition(
                """
                SELECT LearningSenseSources.LearningSenseId, Forms.FormId, Forms.Surface,
                       Forms.FormKind, Forms.FormSourceKind
                FROM LearningSenseSources
                INNER JOIN Senses ON Senses.SenseId = LearningSenseSources.SenseId
                INNER JOIN Forms ON Forms.LexemeId = Senses.LexemeId
                WHERE LearningSenseSources.LearningSenseId IN @LearningSenseIds
                ORDER BY LearningSenseSources.LearningSenseId,
                         LearningSenseSources.SenseId, Forms.FormId;
                """,
                parameters,
                cancellationToken: cancellationToken)))
                .GroupBy(row => row.LearningSenseId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<EnglishLexiconLookupFormPo>)group
                        .DistinctBy(row => (row.Surface, row.FormKind))
                        .Select(row => new EnglishLexiconLookupFormPo(
                            row.Surface,
                            row.FormKind,
                            row.FormSourceKind))
                        .ToArray(),
                    StringComparer.Ordinal);

            return orderedLearningSenseIds
                .Where(learningSenses.ContainsKey)
                .Select(id =>
                {
                    var value = learningSenses[id];
                    return new EnglishLexiconLookupPo(
                        value.LearningSenseId,
                        value.SourceLexemeId,
                        value.Headword,
                        value.PartOfSpeech,
                        value.IsPhrase != 0,
                        value.MeaningSource,
                        value.MeaningPrecision,
                        meanings.TryGetValue(id, out var learningMeanings)
                            ? learningMeanings
                            : Array.Empty<string>(),
                        forms.TryGetValue(id, out var learningForms)
                            ? learningForms
                            : Array.Empty<EnglishLexiconLookupFormPo>());
                })
                .ToArray();
        }

        private sealed class LearningSenseRow
        {
            public string LearningSenseId { get; set; } = string.Empty;

            public string SourceLexemeId { get; set; } = string.Empty;

            public string Headword { get; set; } = string.Empty;

            public string PartOfSpeech { get; set; } = string.Empty;

            public int IsPhrase { get; set; }

            public string MeaningSource { get; set; } = string.Empty;

            public string MeaningPrecision { get; set; } = string.Empty;

            public int SortOrder { get; set; }
        }

        private sealed class MeaningRow
        {
            public string LearningSenseId { get; set; } = string.Empty;

            public int MeaningIndex { get; set; }

            public string Text { get; set; } = string.Empty;
        }

        private sealed class LearningFormRow
        {
            public string LearningSenseId { get; set; } = string.Empty;

            public long FormId { get; set; }

            public string Surface { get; set; } = string.Empty;

            public string FormKind { get; set; } = string.Empty;

            public string FormSourceKind { get; set; } = string.Empty;
        }
    }
}
