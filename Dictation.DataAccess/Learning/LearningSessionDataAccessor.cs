using System.Globalization;
using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Runtime;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Learning
{
    public sealed class LearningSessionDataAccessor
    {
        private static readonly HashSet<string> Languages = new(StringComparer.Ordinal) { "Japanese", "English" };
        private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "InProgress", "Completed" };
        private readonly SqliteConnectionFactory connectionFactory;

        public LearningSessionDataAccessor(SqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<LearningSessionPo?> GetActiveAsync(
            string language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
                    """
                    SELECT SessionId, Language, Status, CurrentOrdinal, CreatedAtUtc, CompletedAtUtc
                    FROM LearningSessions
                    WHERE Language = @Language AND Status = 'InProgress';
                    """,
                    new { Language = language },
                    cancellationToken: cancellationToken));
                return row is null ? null : await MapSessionAsync(connection, row, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LearningSessionDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningSessionDataAccessException("读取活动学习会话失败。", exception);
            }
        }

        public async Task<IReadOnlyList<LearningSessionCandidatePo>> GetCandidatesAsync(
            string language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                var groups = (await connection.QueryAsync<CandidateGroupRow>(new CommandDefinition(
                    """
                    SELECT WordGroups.WordGroupId, WordGroups.CreatedAtUtc,
                           CASE WHEN LearningGroupProgress.WordGroupId IS NULL THEN 0 ELSE 1 END AS HasProgress
                    FROM WordGroups
                    LEFT JOIN LearningGroupProgress
                        ON LearningGroupProgress.WordGroupId = WordGroups.WordGroupId
                    WHERE WordGroups.Language = @Language AND WordGroups.Placement = 'Learning'
                    ORDER BY WordGroups.CreatedAtUtc, WordGroups.WordGroupId;
                    """,
                    new { Language = language },
                    cancellationToken: cancellationToken))).ToArray();
                if (groups.Length == 0)
                {
                    return Array.Empty<LearningSessionCandidatePo>();
                }

                var groupIds = groups.Select(group => group.WordGroupId).ToArray();
                var forms = (await connection.QueryAsync<CandidateFormRow>(new CommandDefinition(
                    """
                    SELECT DISTINCT WordGroupMembers.WordGroupId, WordGroupMembers.MemberId, WordGroupForms.FormCode
                    FROM WordGroupMembers
                    INNER JOIN WordGroupForms ON WordGroupForms.MemberId = WordGroupMembers.MemberId
                    WHERE WordGroupMembers.WordGroupId IN @WordGroupIds
                    ORDER BY WordGroupMembers.WordGroupId, WordGroupMembers.MemberId, WordGroupForms.FormCode;
                    """,
                    new { WordGroupIds = groupIds },
                    cancellationToken: cancellationToken))).ToArray();
                var states = (await connection.QueryAsync<FormStateRow>(new CommandDefinition(
                    """
                    SELECT WordGroupId, MemberId, FormCode, IsMastered
                    FROM LearningFormStates
                    WHERE WordGroupId IN @WordGroupIds
                    ORDER BY WordGroupId, MemberId, FormCode;
                    """,
                    new { WordGroupIds = groupIds },
                    cancellationToken: cancellationToken))).ToArray();
                var formsByGroup = forms.GroupBy(form => form.WordGroupId, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<LearningFormIdentityPo>)group
                            .Select(form => new LearningFormIdentityPo(form.MemberId, form.FormCode)).ToArray(),
                        StringComparer.Ordinal);
                var statesByGroup = states.GroupBy(state => state.WordGroupId, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<LearningFormStateSummaryPo>)group
                            .Select(state => new LearningFormStateSummaryPo(state.MemberId, state.FormCode, state.IsMastered != 0)).ToArray(),
                        StringComparer.Ordinal);
                return groups.Select(group => new LearningSessionCandidatePo(
                    group.WordGroupId,
                    ParseDateTime(group.CreatedAtUtc, "WordGroups.CreatedAtUtc"),
                    group.HasProgress != 0,
                    formsByGroup.TryGetValue(group.WordGroupId, out var groupForms) ? groupForms : Array.Empty<LearningFormIdentityPo>(),
                    statesByGroup.TryGetValue(group.WordGroupId, out var groupStates) ? groupStates : Array.Empty<LearningFormStateSummaryPo>())).ToArray();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LearningSessionDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningSessionDataAccessException("读取学习候选失败。", exception);
            }
        }

        public async Task<int> CountStartedOnDateAsync(
            string language,
            string localDate,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            localDate = RequireText(localDate, nameof(localDate));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    SELECT COUNT(*) FROM LearningGroupProgress
                    WHERE Language = @Language AND FirstStartedLocalDate = @LocalDate;
                    """,
                    new { Language = language, LocalDate = localDate },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningSessionDataAccessException("读取每日学习额度失败。", exception);
            }
        }

        public async Task<LearningSessionPo> CreateAsync(
            CreateLearningSessionPo request,
            CancellationToken cancellationToken = default)
        {
            ValidateCreateRequest(request);
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
                        INSERT INTO LearningSessions (
                            SessionId, Language, Status, CurrentOrdinal, CreatedAtUtc, CompletedAtUtc)
                        VALUES (@SessionId, @Language, @Status, 0, @CreatedAtUtc, @CompletedAtUtc);
                        """,
                        new
                        {
                            request.SessionId,
                            request.Language,
                            request.Status,
                            CreatedAtUtc = FormatDateTime(request.CreatedAtUtc),
                            CompletedAtUtc = request.CompletedAtUtc is null ? null : FormatDateTime(request.CompletedAtUtc.Value)
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                    for (var index = 0; index < request.Groups.Count; index++)
                    {
                        var group = request.Groups[index];
                        var state = await connection.QuerySingleOrDefaultAsync<GroupStateRow>(new CommandDefinition(
                            "SELECT Language, Placement FROM WordGroups WHERE WordGroupId = @WordGroupId;",
                            new { group.WordGroupId },
                            transaction,
                            cancellationToken: cancellationToken));
                        if (state is null
                            || !string.Equals(state.Language, request.Language, StringComparison.Ordinal)
                            || !string.Equals(state.Placement, "Learning", StringComparison.Ordinal))
                        {
                            throw InvalidData("会话包含不存在、其他语言或非学习库词组。");
                        }

                        await connection.ExecuteAsync(new CommandDefinition(
                            "INSERT INTO LearningSessionGroups (SessionId, WordGroupId, SortOrder) VALUES (@SessionId, @WordGroupId, @SortOrder);",
                            new { request.SessionId, group.WordGroupId, SortOrder = index },
                            transaction,
                            cancellationToken: cancellationToken));
                        await connection.ExecuteAsync(new CommandDefinition(
                            """
                            INSERT OR IGNORE INTO LearningGroupProgress (
                                WordGroupId, Language, FirstStartedLocalDate, CompletedLocalDate)
                            VALUES (@WordGroupId, @Language, @FirstStartedLocalDate, NULL);
                            """,
                            new
                            {
                                group.WordGroupId,
                                request.Language,
                                FirstStartedLocalDate = request.StartedLocalDate
                            },
                            transaction,
                            cancellationToken: cancellationToken));
                        foreach (var form in group.Forms)
                        {
                            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                                """
                                SELECT COUNT(*)
                                FROM WordGroupMembers
                                INNER JOIN WordGroupForms ON WordGroupForms.MemberId = WordGroupMembers.MemberId
                                WHERE WordGroupMembers.WordGroupId = @WordGroupId
                                  AND WordGroupMembers.MemberId = @MemberId
                                  AND WordGroupForms.FormCode = @FormCode;
                                """,
                                new { group.WordGroupId, form.MemberId, form.FormCode },
                                transaction,
                                cancellationToken: cancellationToken));
                            if (exists == 0)
                            {
                                throw InvalidData("会话词形不属于指定词组快照。");
                            }

                            await connection.ExecuteAsync(new CommandDefinition(
                                """
                                INSERT OR IGNORE INTO LearningFormStates (WordGroupId, MemberId, FormCode)
                                VALUES (@WordGroupId, @MemberId, @FormCode);
                                """,
                                new { group.WordGroupId, form.MemberId, form.FormCode },
                                transaction,
                                cancellationToken: cancellationToken));
                        }
                    }

                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await TryRollbackAsync(transaction);
                    throw;
                }

                var created = await LoadByIdAsync(connection, request.SessionId, cancellationToken);
                return created ?? throw InvalidData("新建学习会话无法重新读取。");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LearningSessionDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningSessionDataAccessException("创建学习会话失败，事务已回滚。", exception);
            }
        }

        public async Task<IReadOnlyList<LearningBrowseItemPo>> GetBrowseItemsAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            sessionId = RequireText(sessionId, nameof(sessionId));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(*) FROM LearningSessions WHERE SessionId = @SessionId;",
                    new { SessionId = sessionId },
                    cancellationToken: cancellationToken));
                if (exists == 0)
                {
                    throw InvalidData("学习会话不存在。");
                }

                var groups = (await connection.QueryAsync<BrowseGroupRow>(new CommandDefinition(
                    """
                    SELECT LearningSessionGroups.SortOrder, WordGroups.WordGroupId
                    FROM LearningSessionGroups
                    INNER JOIN WordGroups ON WordGroups.WordGroupId = LearningSessionGroups.WordGroupId
                    WHERE LearningSessionGroups.SessionId = @SessionId
                    ORDER BY LearningSessionGroups.SortOrder;
                    """,
                    new { SessionId = sessionId },
                    cancellationToken: cancellationToken))).ToArray();
                return await LoadBrowseSnapshotsAsync(connection, groups, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (LearningSessionDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningSessionDataAccessException("读取本轮浏览内容失败。", exception);
            }
        }

        private static async Task<LearningSessionPo?> LoadByIdAsync(
            SqliteConnection connection,
            string sessionId,
            CancellationToken cancellationToken)
        {
            var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
                """
                SELECT SessionId, Language, Status, CurrentOrdinal, CreatedAtUtc, CompletedAtUtc
                FROM LearningSessions WHERE SessionId = @SessionId;
                """,
                new { SessionId = sessionId },
                cancellationToken: cancellationToken));
            return row is null ? null : await MapSessionAsync(connection, row, cancellationToken);
        }

        private static async Task<LearningSessionPo> MapSessionAsync(
            SqliteConnection connection,
            SessionRow row,
            CancellationToken cancellationToken)
        {
            var groupIds = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT WordGroupId FROM LearningSessionGroups WHERE SessionId = @SessionId ORDER BY SortOrder;",
                new { row.SessionId },
                cancellationToken: cancellationToken))).ToArray();
            return new LearningSessionPo(
                row.SessionId,
                row.Language,
                row.Status,
                row.CurrentOrdinal,
                ParseDateTime(row.CreatedAtUtc, "LearningSessions.CreatedAtUtc"),
                row.CompletedAtUtc is null ? null : ParseDateTime(row.CompletedAtUtc, "LearningSessions.CompletedAtUtc"),
                groupIds);
        }

        private static async Task<IReadOnlyList<LearningBrowseItemPo>> LoadBrowseSnapshotsAsync(
            SqliteConnection connection,
            IReadOnlyList<BrowseGroupRow> groups,
            CancellationToken cancellationToken)
        {
            if (groups.Count == 0)
            {
                return Array.Empty<LearningBrowseItemPo>();
            }

            var groupIds = groups.Select(group => group.WordGroupId).ToArray();
            var members = (await connection.QueryAsync<MemberRow>(new CommandDefinition(
                """
                SELECT MemberId, WordGroupId, Language, SourceSenseId, SourceLexemeId, Headword, Reading,
                       PartOfSpeechLabel, AttributeLabel, IsPhrase, SortOrder,
                       MeaningLanguage, MeaningSource, MeaningPrecision, KanjiUsageCode
                FROM WordGroupMembers WHERE WordGroupId IN @WordGroupIds ORDER BY WordGroupId, SortOrder;
                """,
                new { WordGroupIds = groupIds },
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
                .ToDictionary(group => group.Key, group => group.OrderBy(form => form.FormIndex).Select(form => new ImportWordGroupFormPo(
                    form.Surface, form.Reading, form.FormCode, form.ScriptCode, form.RoleCode,
                    form.FormLabel, form.DetailLabel, form.IsCommon != 0, form.IsPrimary != 0)).ToArray());
            return groups.Select(group => new LearningBrowseItemPo(
                group.SortOrder,
                group.WordGroupId,
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

        private static void ValidateCreateRequest(CreateLearningSessionPo request)
        {
            ArgumentNullException.ThrowIfNull(request);
            RequireText(request.SessionId, nameof(request.SessionId));
            ValidateLanguage(request.Language);
            if (!Statuses.Contains(request.Status))
            {
                throw new ArgumentOutOfRangeException(nameof(request.Status), request.Status, "会话状态不受支持。");
            }

            RequireText(request.StartedLocalDate, nameof(request.StartedLocalDate));
            ArgumentNullException.ThrowIfNull(request.Groups);
            if (request.Status == "InProgress" && request.Groups.Count == 0)
            {
                throw new ArgumentException("进行中的会话至少需要一个词组。", nameof(request.Groups));
            }

            if ((request.Status == "Completed") != request.CompletedAtUtc.HasValue)
            {
                throw new ArgumentException("完成状态与完成时间不一致。", nameof(request));
            }

            if (request.Groups.Select(group => group.WordGroupId).Distinct(StringComparer.Ordinal).Count() != request.Groups.Count)
            {
                throw new ArgumentException("会话词组不能重复。", nameof(request.Groups));
            }

            foreach (var group in request.Groups)
            {
                ArgumentNullException.ThrowIfNull(group);
                RequireText(group.WordGroupId, nameof(group.WordGroupId));
                ArgumentNullException.ThrowIfNull(group.Forms);
                if (group.Forms.Count == 0)
                {
                    throw new ArgumentException("会话词组至少需要一个可练习词形。", nameof(group.Forms));
                }

                if (group.Forms.Select(form => (form.MemberId, form.FormCode)).Distinct().Count() != group.Forms.Count)
                {
                    throw new ArgumentException("会话词形不能重复。", nameof(group.Forms));
                }

                foreach (var form in group.Forms)
                {
                    RequireText(form.FormCode, nameof(form.FormCode));
                }
            }
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

        private static string FormatDateTime(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        private static DateTimeOffset ParseDateTime(string value, string fieldName)
        {
            return DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : throw InvalidData($"数据库字段 {fieldName} 不是有效时间。");
        }

        private static LearningSessionDataAccessException InvalidData(string message) =>
            new(message, new InvalidDataException(message));

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

        private sealed class SessionRow
        {
            public string SessionId { get; set; } = string.Empty;
            public string Language { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int CurrentOrdinal { get; set; }
            public string CreatedAtUtc { get; set; } = string.Empty;
            public string? CompletedAtUtc { get; set; }
        }

        private sealed class CandidateGroupRow
        {
            public string WordGroupId { get; set; } = string.Empty;
            public string CreatedAtUtc { get; set; } = string.Empty;
            public int HasProgress { get; set; }
        }

        private sealed class CandidateFormRow
        {
            public string WordGroupId { get; set; } = string.Empty;
            public long MemberId { get; set; }
            public string FormCode { get; set; } = string.Empty;
        }

        private sealed class FormStateRow
        {
            public string WordGroupId { get; set; } = string.Empty;
            public long MemberId { get; set; }
            public string FormCode { get; set; } = string.Empty;
            public int IsMastered { get; set; }
        }

        private sealed class GroupStateRow
        {
            public string Language { get; set; } = string.Empty;
            public string Placement { get; set; } = string.Empty;
        }

        private sealed class BrowseGroupRow
        {
            public int SortOrder { get; set; }
            public string WordGroupId { get; set; } = string.Empty;
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
