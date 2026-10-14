using System.Globalization;
using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Runtime;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 负责学习计划与学习库分页的持久化访问。
    /// </summary>
    public sealed class LearningPlanDataAccessor
    {
        private static readonly HashSet<string> Languages = new(StringComparer.Ordinal) { "Japanese", "English" };
        private readonly SqliteConnectionFactory connectionFactory;
        private readonly WordGroupMemberSnapshotReader wordGroupMemberSnapshotReader;

        /// <summary>
        /// 使用指定连接工厂和词组成员快照读取组件初始化数据访问器。
        /// </summary>
        /// <param name="connectionFactory">运行数据库连接工厂。</param>
        /// <param name="wordGroupMemberSnapshotReader">复用调用方连接读取词组成员快照的组件。</param>
        internal LearningPlanDataAccessor(
            SqliteConnectionFactory connectionFactory,
            WordGroupMemberSnapshotReader wordGroupMemberSnapshotReader)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.wordGroupMemberSnapshotReader = wordGroupMemberSnapshotReader
                ?? throw new ArgumentNullException(nameof(wordGroupMemberSnapshotReader));
        }

        /// <summary>
        /// 读取指定语言保存的学习计划及选项。
        /// </summary>
        /// <param name="language">持久化使用的语言代码。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        /// <returns>已保存的计划；尚未保存时为空。</returns>
        public async Task<LearningPlanPo?> GetPlanAsync(string language, CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                var row = await connection.QuerySingleOrDefaultAsync<PlanRow>(new CommandDefinition(
                    """
                    SELECT Language, DailyNewTarget, SessionWordLimit, SelectionMode,
                           EnglishFormLabelMode, EnglishExampleSourceSenseId, UpdatedAtUtc
                    FROM LearningPlans WHERE Language = @Language;
                    """,
                    new { Language = language },
                    cancellationToken: cancellationToken));
                if (row is null)
                {
                    return null;
                }

                return new LearningPlanPo(
                    row.Language,
                    row.DailyNewTarget,
                    row.SessionWordLimit,
                    row.SelectionMode,
                    row.EnglishFormLabelMode,
                    row.EnglishExampleSourceSenseId,
                    await ReadValuesAsync(connection, "LearningPlanForms", "FormCode", language, cancellationToken),
                    await ReadValuesAsync(connection, "LearningPlanChannels", "Channel", language, cancellationToken),
                    await ReadValuesAsync(connection, "LearningPlanOutputs", "OutputMode", language, cancellationToken),
                    ParseDateTime(row.UpdatedAtUtc, "LearningPlans.UpdatedAtUtc"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LearningPlanDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningPlanDataAccessException("读取学习计划失败。", exception);
            }
        }

        /// <summary>
        /// 在事务内保存指定语言的学习计划及关联选项。
        /// </summary>
        /// <param name="plan">待保存的学习计划及选项快照。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        public async Task SavePlanAsync(LearningPlanPo plan, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ValidateLanguage(plan.Language);
            ArgumentNullException.ThrowIfNull(plan.FormCodes);
            ArgumentNullException.ThrowIfNull(plan.Channels);
            ArgumentNullException.ThrowIfNull(plan.OutputModes);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO LearningPlans (
                            Language, DailyNewTarget, SessionWordLimit, SelectionMode,
                            EnglishFormLabelMode, EnglishExampleSourceSenseId, UpdatedAtUtc)
                        VALUES (
                            @Language, @DailyNewTarget, @SessionWordLimit, @SelectionMode,
                            @EnglishFormLabelMode, @EnglishExampleSourceSenseId, @UpdatedAtUtc)
                        ON CONFLICT(Language) DO UPDATE SET
                            DailyNewTarget = excluded.DailyNewTarget,
                            SessionWordLimit = excluded.SessionWordLimit,
                            SelectionMode = excluded.SelectionMode,
                            EnglishFormLabelMode = excluded.EnglishFormLabelMode,
                            EnglishExampleSourceSenseId = excluded.EnglishExampleSourceSenseId,
                            UpdatedAtUtc = excluded.UpdatedAtUtc;
                        """,
                        new
                        {
                            plan.Language,
                            plan.DailyNewTarget,
                            plan.SessionWordLimit,
                            plan.SelectionMode,
                            plan.EnglishFormLabelMode,
                            plan.EnglishExampleSourceSenseId,
                            UpdatedAtUtc = FormatDateTime(plan.UpdatedAtUtc)
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                    await ReplaceValuesAsync(connection, transaction, "LearningPlanForms", "FormCode", plan.Language, plan.FormCodes, cancellationToken);
                    await ReplaceValuesAsync(connection, transaction, "LearningPlanChannels", "Channel", plan.Language, plan.Channels, cancellationToken);
                    await ReplaceValuesAsync(connection, transaction, "LearningPlanOutputs", "OutputMode", plan.Language, plan.OutputModes, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await TryRollbackAsync(transaction);
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LearningPlanDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningPlanDataAccessException("保存学习计划失败，事务已回滚。", exception);
            }
        }

        /// <summary>
        /// 读取学习库分页并通过共享读取器补齐成员快照。
        /// </summary>
        /// <param name="language">持久化使用的语言代码。</param>
        /// <param name="offset">从零开始的分页偏移。</param>
        /// <param name="limit">分页大小，范围为 1–500。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        public async Task<LearningLibraryPagePo> GetLearningLibraryPageAsync(
            string language,
            int offset,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 500);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(*) FROM WordGroups WHERE Language = @Language AND Placement = 'Learning';",
                    new { Language = language },
                    cancellationToken: cancellationToken));
                var groups = (await connection.QueryAsync<GroupRow>(new CommandDefinition(
                    """
                    SELECT WordGroupId, Language, AnchorSourceSenseId, CreatedAtUtc
                    FROM WordGroups
                    WHERE Language = @Language AND Placement = 'Learning'
                    ORDER BY CreatedAtUtc, WordGroupId
                    LIMIT @Limit OFFSET @Offset;
                    """,
                    new { Language = language, Limit = limit, Offset = offset },
                    cancellationToken: cancellationToken))).ToArray();
                return new LearningLibraryPagePo(totalCount, await LoadGroupSnapshotsAsync(connection, groups, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LearningPlanDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningPlanDataAccessException("读取学习库失败。", exception);
            }
        }

        /// <summary>
        /// 统计指定标识中仍属于该语言学习库的词组数量。
        /// </summary>
        /// <param name="language">持久化使用的语言代码。</param>
        /// <param name="wordGroupIds">待统计的词组标识集合。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        public async Task<int> CountLearningWordGroupsAsync(
            string language,
            IReadOnlyList<string> wordGroupIds,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            ArgumentNullException.ThrowIfNull(wordGroupIds);
            var distinctIds = wordGroupIds.Select(id => RequireText(id, nameof(wordGroupIds)))
                .Distinct(StringComparer.Ordinal).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            if (distinctIds.Length == 0)
            {
                return 0;
            }

            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COUNT(*) FROM WordGroups
                    WHERE Language = @Language AND Placement = 'Learning' AND WordGroupId IN @WordGroupIds;
                    """,
                    new { Language = language, WordGroupIds = distinctIds },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningPlanDataAccessException("校验学习库选择失败。", exception);
            }
        }

        /// <summary>
        /// 事务性删除学习库词组及其关联状态。
        /// </summary>
        /// <param name="language">持久化使用的语言代码。</param>
        /// <param name="wordGroupId">目标词组标识。</param>
        /// <param name="completedAtUtc">本次删除导致活动会话结算时的 UTC 时间。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        /// <returns>已删除，或未找到、语言不符、位置不符的具体结果。</returns>
        public async Task<DeleteLearningWordGroupResult> DeleteWordGroupAsync(
            string language,
            string wordGroupId,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            wordGroupId = RequireText(wordGroupId, nameof(wordGroupId));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    var row = await connection.QuerySingleOrDefaultAsync<GroupStateRow>(new CommandDefinition(
                        "SELECT Language, Placement FROM WordGroups WHERE WordGroupId = @WordGroupId;",
                        new { WordGroupId = wordGroupId },
                        transaction,
                        cancellationToken: cancellationToken));
                    var result = row switch
                    {
                        null => DeleteLearningWordGroupResult.NotFound,
                        _ when !string.Equals(row.Language, language, StringComparison.Ordinal) => DeleteLearningWordGroupResult.WrongLanguage,
                        _ when !string.Equals(row.Placement, "Learning", StringComparison.Ordinal) => DeleteLearningWordGroupResult.WrongPlacement,
                        _ => DeleteLearningWordGroupResult.Deleted
                    };
                    if (result == DeleteLearningWordGroupResult.Deleted)
                    {
                        var affectedSessionIds = (await connection.QueryAsync<string>(new CommandDefinition(
                            """
                            SELECT LearningSessions.SessionId
                            FROM LearningSessions
                            INNER JOIN LearningSessionGroups
                                ON LearningSessionGroups.SessionId = LearningSessions.SessionId
                            WHERE LearningSessions.Status = 'InProgress'
                              AND LearningSessionGroups.WordGroupId = @WordGroupId;
                            """,
                            new { WordGroupId = wordGroupId },
                            transaction,
                            cancellationToken: cancellationToken))).ToArray();
                        await connection.ExecuteAsync(new CommandDefinition(
                            "DELETE FROM WordGroups WHERE WordGroupId = @WordGroupId;",
                            new { WordGroupId = wordGroupId },
                            transaction,
                            cancellationToken: cancellationToken));
                        if (affectedSessionIds.Length > 0)
                        {
                            // 只清理本次删除导致的空会话，保留其他语言及仍有词组的会话。
                            await connection.ExecuteAsync(new CommandDefinition(
                                """
                                DELETE FROM LearningSessions
                                WHERE SessionId IN @SessionIds
                                  AND Status = 'InProgress'
                                  AND NOT EXISTS (
                                      SELECT 1 FROM LearningSessionGroups
                                      WHERE LearningSessionGroups.SessionId = LearningSessions.SessionId);
                                """,
                                new { SessionIds = affectedSessionIds },
                                transaction,
                                cancellationToken: cancellationToken));
                            await connection.ExecuteAsync(new CommandDefinition(
                                """
                                UPDATE LearningSessions
                                SET Status = 'Completed', CompletedAtUtc = @CompletedAtUtc
                                WHERE SessionId IN @SessionIds
                                  AND Status = 'InProgress'
                                  AND NOT EXISTS (
                                      SELECT 1
                                      FROM LearningSessionGroups AS Groups
                                      INNER JOIN LearningFormStates AS Forms
                                          ON Forms.WordGroupId = Groups.WordGroupId
                                      WHERE Groups.SessionId = LearningSessions.SessionId
                                        AND Forms.IsMastered = 0);
                                """,
                                new
                                {
                                    SessionIds = affectedSessionIds,
                                    CompletedAtUtc = FormatDateTime(completedAtUtc)
                                },
                                transaction,
                                cancellationToken: cancellationToken));
                        }
                    }

                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await TryRollbackAsync(transaction);
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningPlanDataAccessException("删除学习库词组失败。", exception);
            }
        }

        private static async Task<IReadOnlyList<string>> ReadValuesAsync(
            SqliteConnection connection,
            string table,
            string column,
            string language,
            CancellationToken cancellationToken)
        {
            return (await connection.QueryAsync<string>(new CommandDefinition(
                $"SELECT {column} FROM {table} WHERE Language = @Language ORDER BY SortOrder;",
                new { Language = language },
                cancellationToken: cancellationToken))).ToArray();
        }

        private static async Task ReplaceValuesAsync(
            SqliteConnection connection,
            System.Data.Common.DbTransaction transaction,
            string table,
            string column,
            string language,
            IReadOnlyList<string> values,
            CancellationToken cancellationToken)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"DELETE FROM {table} WHERE Language = @Language;",
                new { Language = language },
                transaction,
                cancellationToken: cancellationToken));
            for (var index = 0; index < values.Count; index++)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    $"INSERT INTO {table} (Language, SortOrder, {column}) VALUES (@Language, @SortOrder, @Value);",
                    new { Language = language, SortOrder = index, Value = values[index] },
                    transaction,
                    cancellationToken: cancellationToken));
            }
        }

        private async Task<IReadOnlyList<LearningWordGroupPo>> LoadGroupSnapshotsAsync(
            SqliteConnection connection,
            IReadOnlyList<GroupRow> groups,
            CancellationToken cancellationToken)
        {
            if (groups.Count == 0)
            {
                return Array.Empty<LearningWordGroupPo>();
            }

            var membersByGroup = await wordGroupMemberSnapshotReader.ReadAsync(
                connection,
                transaction: null,
                groups.Select(group => group.WordGroupId).ToArray(),
                cancellationToken);

            return groups.Select(group => new LearningWordGroupPo(
                group.WordGroupId,
                group.Language,
                group.AnchorSourceSenseId,
                ParseDateTime(group.CreatedAtUtc, "WordGroups.CreatedAtUtc"),
                membersByGroup.TryGetValue(group.WordGroupId, out var groupMembers)
                    ? groupMembers
                    : Array.Empty<ImportWordGroupMemberPo>())).ToArray();
        }

        private static void ValidateLanguage(string language)
        {
            if (!Languages.Contains(language))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "学习语言不受支持。");
            }
        }

        private static string RequireText(string value, string parameterName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
            return value.Trim();
        }

        private static DateTimeOffset ParseDateTime(string value, string fieldName)
        {
            return DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : throw new LearningPlanDataAccessException($"数据库字段 {fieldName} 不是有效时间。", new FormatException(value));
        }

        private static string FormatDateTime(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        private static async Task TryRollbackAsync(System.Data.Common.DbTransaction transaction)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // 保留原始数据库异常。
            }
        }

        private sealed class PlanRow
        {
            public string Language { get; set; } = string.Empty;
            public int DailyNewTarget { get; set; }
            public int SessionWordLimit { get; set; }
            public string SelectionMode { get; set; } = string.Empty;
            public string EnglishFormLabelMode { get; set; } = string.Empty;
            public string? EnglishExampleSourceSenseId { get; set; }
            public string UpdatedAtUtc { get; set; } = string.Empty;
        }

        private sealed class GroupRow
        {
            public string WordGroupId { get; set; } = string.Empty;
            public string Language { get; set; } = string.Empty;
            public string AnchorSourceSenseId { get; set; } = string.Empty;
            public string CreatedAtUtc { get; set; } = string.Empty;
        }

        private sealed class GroupStateRow
        {
            public string Language { get; set; } = string.Empty;
            public string Placement { get; set; } = string.Empty;
        }

    }
}
