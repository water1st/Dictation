using System.Globalization;
using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Runtime;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Learning
{
    public sealed class LearningPlanDataAccessor
    {
        private static readonly HashSet<string> Languages = new(StringComparer.Ordinal) { "Japanese", "English" };
        private readonly SqliteConnectionFactory connectionFactory;

        public LearningPlanDataAccessor(SqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

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

        public async Task<DeleteLearningWordGroupResult> DeleteWordGroupAsync(
            string language,
            string wordGroupId,
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
                        await connection.ExecuteAsync(new CommandDefinition(
                            "DELETE FROM WordGroups WHERE WordGroupId = @WordGroupId;",
                            new { WordGroupId = wordGroupId },
                            transaction,
                            cancellationToken: cancellationToken));
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

        private static async Task<IReadOnlyList<LearningWordGroupPo>> LoadGroupSnapshotsAsync(
            SqliteConnection connection,
            IReadOnlyList<GroupRow> groups,
            CancellationToken cancellationToken)
        {
            if (groups.Count == 0)
            {
                return Array.Empty<LearningWordGroupPo>();
            }

            var groupIds = groups.Select(group => group.WordGroupId).ToArray();
            var members = (await connection.QueryAsync<MemberRow>(new CommandDefinition(
                """
                SELECT MemberId, WordGroupId, Language, SourceSenseId, SourceLexemeId, Headword, Reading,
                       PartOfSpeechLabel, AttributeLabel, IsPhrase, SortOrder,
                       MeaningLanguage, MeaningSource, MeaningPrecision, KanjiUsageCode
                FROM WordGroupMembers WHERE WordGroupId IN @GroupIds ORDER BY WordGroupId, SortOrder;
                """,
                new { GroupIds = groupIds },
                cancellationToken: cancellationToken))).ToArray();
            var memberIds = members.Select(member => member.MemberId).ToArray();
            var meanings = memberIds.Length == 0 ? Array.Empty<MeaningRow>() :
                (await connection.QueryAsync<MeaningRow>(new CommandDefinition(
                    "SELECT MemberId, MeaningIndex, Text FROM WordGroupMeanings WHERE MemberId IN @MemberIds ORDER BY MemberId, MeaningIndex;",
                    new { MemberIds = memberIds }, cancellationToken: cancellationToken))).ToArray();
            var forms = memberIds.Length == 0 ? Array.Empty<FormRow>() :
                (await connection.QueryAsync<FormRow>(new CommandDefinition(
                    """
                    SELECT MemberId, FormIndex, Surface, Reading, FormCode, ScriptCode, RoleCode,
                           FormLabel, DetailLabel, IsCommon, IsPrimary
                    FROM WordGroupForms WHERE MemberId IN @MemberIds ORDER BY MemberId, FormIndex;
                    """,
                    new { MemberIds = memberIds }, cancellationToken: cancellationToken))).ToArray();
            var membersByGroup = members.GroupBy(member => member.WordGroupId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderBy(member => member.SortOrder).ToArray(), StringComparer.Ordinal);
            var meaningsByMember = meanings.GroupBy(meaning => meaning.MemberId)
                .ToDictionary(group => group.Key, group => group.OrderBy(meaning => meaning.MeaningIndex).Select(meaning => meaning.Text).ToArray());
            var formsByMember = forms.GroupBy(form => form.MemberId)
                .ToDictionary(group => group.Key, group => group.OrderBy(form => form.FormIndex).Select(MapForm).ToArray());

            return groups.Select(group => new LearningWordGroupPo(
                group.WordGroupId,
                group.Language,
                group.AnchorSourceSenseId,
                ParseDateTime(group.CreatedAtUtc, "WordGroups.CreatedAtUtc"),
                membersByGroup.TryGetValue(group.WordGroupId, out var groupMembers)
                    ? groupMembers.Select(member => new ImportWordGroupMemberPo(
                        member.Language, member.SourceSenseId, member.SourceLexemeId, member.Headword, member.Reading,
                        member.PartOfSpeechLabel, member.AttributeLabel, member.IsPhrase != 0,
                        member.MeaningLanguage, member.MeaningSource, member.MeaningPrecision,
                        meaningsByMember.TryGetValue(member.MemberId, out var memberMeanings) ? memberMeanings : Array.Empty<string>(),
                        formsByMember.TryGetValue(member.MemberId, out var memberForms) ? memberForms : Array.Empty<ImportWordGroupFormPo>(),
                        member.KanjiUsageCode)).ToArray()
                    : Array.Empty<ImportWordGroupMemberPo>())).ToArray();
        }

        private static ImportWordGroupFormPo MapForm(FormRow form)
        {
            return new ImportWordGroupFormPo(form.Surface, form.Reading, form.FormCode, form.ScriptCode, form.RoleCode,
                form.FormLabel, form.DetailLabel, form.IsCommon != 0, form.IsPrimary != 0);
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

        private sealed class MemberRow
        {
            public long MemberId { get; set; }
            public string WordGroupId { get; set; } = string.Empty;
            public string Language { get; set; } = string.Empty;
            public string SourceSenseId { get; set; } = string.Empty;
            public string SourceLexemeId { get; set; } = string.Empty;
            public string Headword { get; set; } = string.Empty;
            public string? Reading { get; set; }
            public string PartOfSpeechLabel { get; set; } = string.Empty;
            public string AttributeLabel { get; set; } = string.Empty;
            public int IsPhrase { get; set; }
            public int SortOrder { get; set; }
            public string MeaningLanguage { get; set; } = string.Empty;
            public string MeaningSource { get; set; } = string.Empty;
            public string MeaningPrecision { get; set; } = string.Empty;
            public string? KanjiUsageCode { get; set; }
        }

        private sealed class MeaningRow
        {
            public long MemberId { get; set; }
            public int MeaningIndex { get; set; }
            public string Text { get; set; } = string.Empty;
        }

        private sealed class FormRow
        {
            public long MemberId { get; set; }
            public int FormIndex { get; set; }
            public string Surface { get; set; } = string.Empty;
            public string? Reading { get; set; }
            public string FormCode { get; set; } = string.Empty;
            public string? ScriptCode { get; set; }
            public string? RoleCode { get; set; }
            public string FormLabel { get; set; } = string.Empty;
            public string DetailLabel { get; set; } = string.Empty;
            public int IsCommon { get; set; }
            public int IsPrimary { get; set; }
        }
    }
}
