using System.Globalization;
using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Runtime;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 负责学习会话、题目状态和会话浏览快照的持久化访问。
    /// </summary>
    public sealed class LearningSessionDataAccessor
    {
        private static readonly HashSet<string> Languages = new(StringComparer.Ordinal) { "Japanese", "English" };
        private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "InProgress", "Completed" };
        private readonly SqliteConnectionFactory connectionFactory;
        private readonly WordGroupMemberSnapshotReader wordGroupMemberSnapshotReader;

        /// <summary>
        /// 使用指定连接工厂和词组成员快照读取组件初始化数据访问器。
        /// </summary>
        /// <param name="connectionFactory">运行数据库连接工厂。</param>
        /// <param name="wordGroupMemberSnapshotReader">复用调用方连接读取词组成员快照的组件。</param>
        internal LearningSessionDataAccessor(
            SqliteConnectionFactory connectionFactory,
            WordGroupMemberSnapshotReader wordGroupMemberSnapshotReader)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.wordGroupMemberSnapshotReader = wordGroupMemberSnapshotReader
                ?? throw new ArgumentNullException(nameof(wordGroupMemberSnapshotReader));
        }

        /// <summary>
        /// 读取指定语言尚未完成的学习会话。
        /// </summary>
        /// <param name="language">持久化使用的语言代码。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        /// <returns>未完成会话；不存在时为空。</returns>
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
                var row = await connection.QuerySingleOrDefaultAsync<LearningSessionRow>(new CommandDefinition(
                    """
                    SELECT SessionId, Language, Status, CurrentOrdinal, CreatedAtUtc, CompletedAtUtc
                    FROM LearningSessions
                    WHERE Language = @Language AND Status = 'InProgress';
                    """,
                    new { Language = language },
                    cancellationToken: cancellationToken));
                return row is null ? null : await LoadSessionAsync(connection, row, cancellationToken);
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

        /// <summary>
        /// 读取指定语言学习库的会话候选词组与累计状态。
        /// </summary>
        /// <param name="language">持久化使用的语言代码。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
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
                    WHERE WordGroups.Language = @Language
                      AND (WordGroups.Placement = 'Learning'
                           OR (WordGroups.Placement = 'Review' AND LearningGroupProgress.CompletedLocalDate IS NOT NULL))
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
                    SELECT DISTINCT WordGroupMembers.WordGroupId, WordGroupMembers.MemberId,
                                    WordGroupForms.FormCode, WordGroupForms.ScriptCode,
                                    WordGroupMembers.KanjiUsageCode
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
                            .Select(form => new LearningFormIdentityPo(
                                form.MemberId,
                                form.FormCode,
                                form.ScriptCode,
                                form.KanjiUsageCode)).ToArray(),
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

        /// <summary>
        /// 统计指定本地日期已开始的学习词组数量。
        /// </summary>
        /// <param name="language">持久化使用的语言代码。</param>
        /// <param name="localDate">用于统计的本地日期文本。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
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

        /// <summary>
        /// 按语言和本地日期读取首次开始及完成的不同词组数量。
        /// </summary>
        /// <param name="language">持久化使用的语言代码。</param>
        /// <param name="localDate">要查询的本地日期文本。</param>
        /// <param name="cancellationToken">取消数据库查询的令牌。</param>
        /// <returns>当天首次开始及完成的词组数量。</returns>
        public async Task<LearningDailyProgressCountsPo> GetDailyProgressCountsAsync(
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
                return await connection.QuerySingleAsync<LearningDailyProgressCountsPo>(new CommandDefinition(
                    """
                    SELECT
                        COUNT(CASE WHEN FirstStartedLocalDate = @LocalDate THEN 1 END) AS StartedGroupCount,
                        COUNT(CASE WHEN CompletedLocalDate = @LocalDate THEN 1 END) AS CompletedGroupCount
                    FROM LearningGroupProgress
                    WHERE Language = @Language;
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
                throw new LearningSessionDataAccessException("读取每日学习进度失败。", exception);
            }
        }

        /// <summary>
        /// 在事务内创建学习会话及词组关联快照。
        /// </summary>
        /// <param name="request">已由业务层准备的持久化请求。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
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
                            """
                            SELECT WordGroups.Language, WordGroups.Placement,
                                   CASE WHEN LearningGroupProgress.CompletedLocalDate IS NULL THEN 0 ELSE 1 END AS HasCompletedProgress
                            FROM WordGroups
                            LEFT JOIN LearningGroupProgress
                                ON LearningGroupProgress.WordGroupId = WordGroups.WordGroupId
                            WHERE WordGroups.WordGroupId = @WordGroupId;
                            """,
                            new { group.WordGroupId },
                            transaction,
                            cancellationToken: cancellationToken));
                        if (state is null
                            || !string.Equals(state.Language, request.Language, StringComparison.Ordinal)
                            || (!string.Equals(state.Placement, "Learning", StringComparison.Ordinal)
                                && !(string.Equals(state.Placement, "Review", StringComparison.Ordinal)
                                    && state.HasCompletedProgress != 0)))
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

        /// <summary>
        /// 读取会话浏览所需的词组和成员快照。
        /// </summary>
        /// <param name="sessionId">目标学习会话标识。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
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

        /// <summary>
        /// 读取会话、待答题及出题状态，供业务层决定下一题。
        /// </summary>
        /// <param name="sessionId">目标学习会话标识。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        /// <returns>完整调度快照；会话不存在时抛出会话访问异常。</returns>
        public async Task<LearningSchedulingSnapshotPo> GetSchedulingSnapshotAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            sessionId = RequireText(sessionId, nameof(sessionId));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                var session = await LoadByIdAsync(connection, sessionId, cancellationToken)
                    ?? throw InvalidData("学习会话不存在。");
                var pendingRow = await connection.QuerySingleOrDefaultAsync<QuestionRow>(new CommandDefinition(
                    """
                    SELECT QuestionId, SessionId, WordGroupId, MemberId, FormCode,
                           Channel, OutputMode, QuestionOrdinal
                    FROM LearningQuestions
                    WHERE SessionId = @SessionId AND Status = 'Pending';
                    """,
                    new { SessionId = sessionId },
                    cancellationToken: cancellationToken));
                var unitRows = (await connection.QueryAsync<FormUnitRow>(new CommandDefinition(
                    """
                    SELECT States.WordGroupId, States.MemberId, States.FormCode,
                           COALESCE((
                               SELECT FormLabel FROM WordGroupForms
                               WHERE MemberId = States.MemberId AND FormCode = States.FormCode
                               ORDER BY IsPrimary DESC, IsCommon DESC, FormIndex LIMIT 1
                           ), States.FormCode) AS FormLabel,
                           Members.IsPhrase, Members.KanjiUsageCode, Members.MeaningLanguage,
                           States.CorrectStreak, States.IsMastered, States.NextEligibleOrdinal,
                           States.MeaningWritingCount, States.DictationCount,
                           States.LatinCount, States.KanaCount, States.KanjiCount,
                           States.CorrectCount, States.ErrorCount
                    FROM LearningSessionGroups AS SessionGroups
                    INNER JOIN LearningFormStates AS States
                        ON States.WordGroupId = SessionGroups.WordGroupId
                    INNER JOIN WordGroupMembers AS Members
                        ON Members.MemberId = States.MemberId
                    WHERE SessionGroups.SessionId = @SessionId
                    ORDER BY SessionGroups.SortOrder, Members.SortOrder, States.FormCode;
                    """,
                    new { SessionId = sessionId },
                    cancellationToken: cancellationToken))).ToArray();
                var memberIds = unitRows.Select(row => row.MemberId).Distinct().ToArray();
                var meanings = memberIds.Length == 0
                    ? Array.Empty<WordGroupMeaningRow>()
                    : (await connection.QueryAsync<WordGroupMeaningRow>(new CommandDefinition(
                        """
                        SELECT MemberId, MeaningIndex, Text
                        FROM WordGroupMeanings
                        WHERE MemberId IN @MemberIds
                        ORDER BY MemberId, MeaningIndex;
                        """,
                        new { MemberIds = memberIds },
                        cancellationToken: cancellationToken))).ToArray();
                var variants = memberIds.Length == 0
                    ? Array.Empty<AnswerVariantRow>()
                    : (await connection.QueryAsync<AnswerVariantRow>(new CommandDefinition(
                        """
                        SELECT MemberId, FormCode, Surface, Reading, ScriptCode, IsCommon, IsPrimary, FormIndex
                        FROM WordGroupForms
                        WHERE MemberId IN @MemberIds
                        ORDER BY MemberId, FormCode, IsPrimary DESC, IsCommon DESC, FormIndex;
                        """,
                        new { MemberIds = memberIds },
                        cancellationToken: cancellationToken))).ToArray();
                var historyRows = (await connection.QueryAsync<QuestionHistoryRow>(new CommandDefinition(
                    """
                    SELECT Questions.QuestionId, Questions.WordGroupId, Questions.MemberId,
                           Questions.FormCode, Questions.Channel, Questions.OutputMode,
                           Questions.QuestionOrdinal, Attempts.WasCorrect
                    FROM LearningQuestions AS Questions
                    LEFT JOIN LearningQuestionAttempts AS Attempts
                        ON Attempts.QuestionId = Questions.QuestionId
                    WHERE Questions.SessionId = @SessionId
                    ORDER BY Questions.QuestionOrdinal;
                    """,
                    new { SessionId = sessionId },
                    cancellationToken: cancellationToken))).ToArray();
                var meaningsByMember = meanings.GroupBy(row => row.MemberId)
                    .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(row => row.Text).ToArray());
                var variantsByUnit = variants.GroupBy(row => (row.MemberId, row.FormCode))
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<LearningAnswerVariantPo>)group.Select(row => new LearningAnswerVariantPo(
                            row.Surface,
                            row.Reading,
                            row.ScriptCode,
                            row.IsCommon != 0,
                            row.IsPrimary != 0)).ToArray());
                var units = unitRows.Select(row => new LearningFormUnitPo(
                    row.WordGroupId,
                    row.MemberId,
                    row.FormCode,
                    row.FormLabel,
                    row.IsPhrase != 0,
                    row.KanjiUsageCode,
                    string.Equals(row.MeaningLanguage, "zh-CN", StringComparison.Ordinal)
                        && meaningsByMember.TryGetValue(row.MemberId, out var memberMeanings)
                            ? memberMeanings
                            : Array.Empty<string>(),
                    variantsByUnit.TryGetValue((row.MemberId, row.FormCode), out var unitVariants)
                        ? unitVariants
                        : Array.Empty<LearningAnswerVariantPo>(),
                    row.CorrectStreak,
                    row.IsMastered != 0,
                    row.NextEligibleOrdinal,
                    row.MeaningWritingCount,
                    row.DictationCount,
                    row.LatinCount,
                    row.KanaCount,
                    row.KanjiCount,
                    row.CorrectCount,
                    row.ErrorCount)).ToArray();
                return new LearningSchedulingSnapshotPo(
                    session,
                    pendingRow is null ? null : MapQuestion(pendingRow),
                    units,
                    historyRows.Select(row => new LearningQuestionHistoryPo(
                        row.QuestionId,
                        row.WordGroupId,
                        row.MemberId,
                        row.FormCode,
                        row.Channel,
                        row.OutputMode,
                        row.QuestionOrdinal,
                        row.WasCorrect is null ? null : row.WasCorrect != 0)).ToArray());
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
                throw new LearningSessionDataAccessException("读取学习题目调度快照失败。", exception);
            }
        }

        /// <summary>
        /// 事务性写入待答题并更新对应出题状态。
        /// </summary>
        /// <param name="question">待持久化的题目和调度状态。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        public async Task<LearningQuestionStatePo> CreatePendingQuestionAsync(
            CreateLearningQuestionPo question,
            CancellationToken cancellationToken = default)
        {
            ValidateQuestion(question);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await ValidateQuestionTargetAsync(connection, transaction, question, cancellationToken);
                    var questionId = await InsertQuestionAsync(connection, transaction, question, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return new LearningQuestionStatePo(
                        questionId,
                        question.SessionId,
                        question.WordGroupId,
                        question.MemberId,
                        question.FormCode,
                        question.Channel,
                        question.OutputMode,
                        question.QuestionOrdinal);
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
            catch (LearningSessionDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningSessionDataAccessException("创建学习待答题失败，事务已回滚。", exception);
            }
        }

        /// <summary>
        /// 在事务内保存答案结果并推进词组与会话状态。
        /// </summary>
        /// <param name="answer">待持久化的答案与状态变化。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
        /// <returns>本次写入的下一道题；没有下一题时为空。</returns>
        public async Task<LearningQuestionStatePo?> ApplyAnswerAsync(
            ApplyLearningAnswerPo answer,
            CancellationToken cancellationToken = default)
        {
            ValidateAnswer(answer);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    var target = await connection.QuerySingleOrDefaultAsync<AnswerTargetRow>(new CommandDefinition(
                        """
                        SELECT Questions.QuestionId, Questions.SessionId, Questions.WordGroupId,
                               Questions.MemberId, Questions.FormCode, Questions.Channel,
                               Questions.OutputMode, Questions.QuestionOrdinal,
                               Questions.Status AS QuestionStatus,
                               Sessions.Status AS SessionStatus, Sessions.CurrentOrdinal
                        FROM LearningQuestions AS Questions
                        INNER JOIN LearningSessions AS Sessions ON Sessions.SessionId = Questions.SessionId
                        WHERE Questions.SessionId = @SessionId AND Questions.QuestionId = @QuestionId;
                        """,
                        new { answer.SessionId, answer.QuestionId },
                        transaction,
                        cancellationToken: cancellationToken));
                    if (target is null
                        || !string.Equals(target.QuestionStatus, "Pending", StringComparison.Ordinal)
                        || !string.Equals(target.SessionStatus, "InProgress", StringComparison.Ordinal)
                        || target.CurrentOrdinal != target.QuestionOrdinal)
                    {
                        throw InvalidData("题目已过期、不属于当前会话或会话已完成。");
                    }

                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO LearningQuestionAttempts (
                            QuestionId, SubmittedText, WasCorrect, AnsweredAtUtc)
                        VALUES (@QuestionId, @SubmittedText, @WasCorrect, @AnsweredAtUtc);
                        """,
                        new
                        {
                            answer.QuestionId,
                            answer.SubmittedText,
                            WasCorrect = answer.WasCorrect ? 1 : 0,
                            AnsweredAtUtc = FormatDateTime(answer.AnsweredAtUtc)
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                    await connection.ExecuteAsync(new CommandDefinition(
                        "UPDATE LearningQuestions SET Status = 'Answered' WHERE QuestionId = @QuestionId;",
                        new { answer.QuestionId },
                        transaction,
                        cancellationToken: cancellationToken));
                    var channelColumn = target.Channel switch
                    {
                        "MeaningWriting" => "MeaningWritingCount",
                        "Dictation" => "DictationCount",
                        _ => throw InvalidData("待答题题型无效。")
                    };
                    var outputColumn = target.OutputMode switch
                    {
                        "Latin" => "LatinCount",
                        "Kana" => "KanaCount",
                        "Kanji" => "KanjiCount",
                        _ => throw InvalidData("待答题输出方式无效。")
                    };
                    var updated = await connection.ExecuteAsync(new CommandDefinition(
                        $"""
                        UPDATE LearningFormStates
                        SET CorrectStreak = @CorrectStreak,
                            IsMastered = @IsMastered,
                            NextEligibleOrdinal = @NextEligibleOrdinal,
                            {channelColumn} = {channelColumn} + 1,
                            {outputColumn} = {outputColumn} + 1,
                            CorrectCount = CorrectCount + @CorrectIncrement,
                            ErrorCount = ErrorCount + @ErrorIncrement,
                            LastAnsweredAtUtc = @AnsweredAtUtc
                        WHERE WordGroupId = @WordGroupId
                          AND MemberId = @MemberId
                          AND FormCode = @FormCode;
                        """,
                        new
                        {
                            target.WordGroupId,
                            target.MemberId,
                            target.FormCode,
                            answer.CorrectStreak,
                            IsMastered = answer.IsMastered ? 1 : 0,
                            answer.NextEligibleOrdinal,
                            CorrectIncrement = answer.WasCorrect ? 1 : 0,
                            ErrorIncrement = answer.WasCorrect ? 0 : 1,
                            AnsweredAtUtc = FormatDateTime(answer.AnsweredAtUtc)
                        },
                        transaction,
                        cancellationToken: cancellationToken));
                    if (updated != 1)
                    {
                        throw InvalidData("待答题对应的学习词形状态不存在。");
                    }

                    var nextOrdinal = target.QuestionOrdinal + 1;
                    await connection.ExecuteAsync(new CommandDefinition(
                        "UPDATE LearningSessions SET CurrentOrdinal = @CurrentOrdinal WHERE SessionId = @SessionId;",
                        new { CurrentOrdinal = nextOrdinal, answer.SessionId },
                        transaction,
                        cancellationToken: cancellationToken));
                    if (answer.CompletedWordGroupIds.Count > 0)
                    {
                        var completedGroupCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                            """
                            SELECT COUNT(*) FROM LearningSessionGroups
                            WHERE SessionId = @SessionId AND WordGroupId IN @WordGroupIds;
                            """,
                            new { answer.SessionId, WordGroupIds = answer.CompletedWordGroupIds },
                            transaction,
                            cancellationToken: cancellationToken));
                        if (completedGroupCount != answer.CompletedWordGroupIds.Count)
                        {
                            throw InvalidData("完成词组不属于当前会话。");
                        }

                        await connection.ExecuteAsync(new CommandDefinition(
                            "UPDATE WordGroups SET Placement = 'Review' WHERE WordGroupId IN @WordGroupIds;",
                            new { WordGroupIds = answer.CompletedWordGroupIds },
                            transaction,
                            cancellationToken: cancellationToken));
                        await connection.ExecuteAsync(new CommandDefinition(
                            """
                            UPDATE LearningGroupProgress
                            SET CompletedLocalDate = @CompletedLocalDate
                            WHERE WordGroupId IN @WordGroupIds;
                            """,
                            new
                            {
                                CompletedLocalDate = answer.AnsweredLocalDate,
                                WordGroupIds = answer.CompletedWordGroupIds
                            },
                            transaction,
                            cancellationToken: cancellationToken));
                    }

                    LearningQuestionStatePo? next = null;
                    if (answer.CompleteSession)
                    {
                        await connection.ExecuteAsync(new CommandDefinition(
                            """
                            UPDATE LearningSessions
                            SET Status = 'Completed', CompletedAtUtc = @CompletedAtUtc
                            WHERE SessionId = @SessionId;
                            """,
                            new { CompletedAtUtc = FormatDateTime(answer.AnsweredAtUtc), answer.SessionId },
                            transaction,
                            cancellationToken: cancellationToken));
                    }
                    else
                    {
                        var nextQuestion = answer.NextQuestion
                            ?? throw InvalidData("未完成会话必须提供下一道题。");
                        if (!string.Equals(nextQuestion.SessionId, answer.SessionId, StringComparison.Ordinal)
                            || nextQuestion.QuestionOrdinal != nextOrdinal)
                        {
                            throw InvalidData("下一道题不属于当前会话或题号不连续。");
                        }

                        ValidateQuestion(nextQuestion);
                        await ValidateQuestionTargetAsync(connection, transaction, nextQuestion, cancellationToken);
                        var nextQuestionId = await InsertQuestionAsync(connection, transaction, nextQuestion, cancellationToken);
                        next = new LearningQuestionStatePo(
                            nextQuestionId,
                            nextQuestion.SessionId,
                            nextQuestion.WordGroupId,
                            nextQuestion.MemberId,
                            nextQuestion.FormCode,
                            nextQuestion.Channel,
                            nextQuestion.OutputMode,
                            nextQuestion.QuestionOrdinal);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    return next;
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
            catch (LearningSessionDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new LearningSessionDataAccessException("提交学习答案失败，事务已回滚。", exception);
            }
        }

        private static async Task<LearningSessionPo?> LoadByIdAsync(
            SqliteConnection connection,
            string sessionId,
            CancellationToken cancellationToken)
        {
            var row = await connection.QuerySingleOrDefaultAsync<LearningSessionRow>(new CommandDefinition(
                """
                SELECT SessionId, Language, Status, CurrentOrdinal, CreatedAtUtc, CompletedAtUtc
                FROM LearningSessions WHERE SessionId = @SessionId;
                """,
                new { SessionId = sessionId },
                cancellationToken: cancellationToken));
            return row is null ? null : await LoadSessionAsync(connection, row, cancellationToken);
        }

        private static async Task<LearningSessionPo> LoadSessionAsync(
            SqliteConnection connection,
            LearningSessionRow row,
            CancellationToken cancellationToken)
        {
            var groupIds = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT WordGroupId FROM LearningSessionGroups WHERE SessionId = @SessionId ORDER BY SortOrder;",
                new { row.SessionId },
                cancellationToken: cancellationToken))).ToArray();
            return LearningSessionMapper.Map(row, groupIds);
        }

        private async Task<IReadOnlyList<LearningBrowseItemPo>> LoadBrowseSnapshotsAsync(
            SqliteConnection connection,
            IReadOnlyList<BrowseGroupRow> groups,
            CancellationToken cancellationToken)
        {
            if (groups.Count == 0)
            {
                return Array.Empty<LearningBrowseItemPo>();
            }

            var membersByGroup = await wordGroupMemberSnapshotReader.ReadAsync(
                connection,
                transaction: null,
                groups.Select(group => group.WordGroupId).ToArray(),
                cancellationToken);
            return groups.Select(group => new LearningBrowseItemPo(
                group.SortOrder,
                group.WordGroupId,
                membersByGroup.TryGetValue(group.WordGroupId, out var groupMembers)
                    ? groupMembers
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

        private static LearningQuestionStatePo MapQuestion(QuestionRow row)
        {
            return new LearningQuestionStatePo(
                row.QuestionId,
                row.SessionId,
                row.WordGroupId,
                row.MemberId,
                row.FormCode,
                row.Channel,
                row.OutputMode,
                row.QuestionOrdinal);
        }

        private static void ValidateQuestion(CreateLearningQuestionPo question)
        {
            ArgumentNullException.ThrowIfNull(question);
            RequireText(question.SessionId, nameof(question.SessionId));
            RequireText(question.WordGroupId, nameof(question.WordGroupId));
            RequireText(question.FormCode, nameof(question.FormCode));
            if (question.MemberId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(question.MemberId));
            }

            if (question.Channel is not ("MeaningWriting" or "Dictation"))
            {
                throw new ArgumentOutOfRangeException(nameof(question.Channel), question.Channel, "题型不受支持。");
            }

            if (question.OutputMode is not ("Latin" or "Kana" or "Kanji"))
            {
                throw new ArgumentOutOfRangeException(nameof(question.OutputMode), question.OutputMode, "输出方式不受支持。");
            }

            ArgumentOutOfRangeException.ThrowIfNegative(question.QuestionOrdinal);
        }

        private static void ValidateAnswer(ApplyLearningAnswerPo answer)
        {
            ArgumentNullException.ThrowIfNull(answer);
            RequireText(answer.SessionId, nameof(answer.SessionId));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(answer.QuestionId);
            ArgumentNullException.ThrowIfNull(answer.SubmittedText);
            if (answer.CorrectStreak is < 0 or > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(answer.CorrectStreak));
            }

            if (answer.IsMastered != (answer.CorrectStreak == 2))
            {
                throw new ArgumentException("掌握状态与连续正确次数不一致。", nameof(answer));
            }

            ArgumentOutOfRangeException.ThrowIfNegative(answer.NextEligibleOrdinal);
            RequireText(answer.AnsweredLocalDate, nameof(answer.AnsweredLocalDate));
            ArgumentNullException.ThrowIfNull(answer.CompletedWordGroupIds);
            if (answer.CompletedWordGroupIds.Distinct(StringComparer.Ordinal).Count() != answer.CompletedWordGroupIds.Count)
            {
                throw new ArgumentException("完成词组不能重复。", nameof(answer.CompletedWordGroupIds));
            }

            if (answer.CompleteSession && answer.NextQuestion is not null)
            {
                throw new ArgumentException("已完成会话不能包含下一道题。", nameof(answer));
            }
        }

        private static async Task ValidateQuestionTargetAsync(
            SqliteConnection connection,
            System.Data.Common.DbTransaction transaction,
            CreateLearningQuestionPo question,
            CancellationToken cancellationToken)
        {
            var session = await connection.QuerySingleOrDefaultAsync<SessionStateRow>(new CommandDefinition(
                "SELECT Status, CurrentOrdinal FROM LearningSessions WHERE SessionId = @SessionId;",
                new { question.SessionId },
                transaction,
                cancellationToken: cancellationToken));
            if (session is null
                || !string.Equals(session.Status, "InProgress", StringComparison.Ordinal)
                || session.CurrentOrdinal != question.QuestionOrdinal)
            {
                throw InvalidData("会话不存在、已完成或当前题号不匹配。");
            }

            var targetCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM LearningSessionGroups AS Groups
                INNER JOIN LearningFormStates AS States
                    ON States.WordGroupId = Groups.WordGroupId
                WHERE Groups.SessionId = @SessionId
                  AND States.WordGroupId = @WordGroupId
                  AND States.MemberId = @MemberId
                  AND States.FormCode = @FormCode
                  AND EXISTS (
                      SELECT 1 FROM WordGroupForms AS Forms
                      WHERE Forms.MemberId = States.MemberId
                        AND Forms.FormCode = States.FormCode
                        AND (@OutputMode = 'Latin' OR Forms.ScriptCode = @OutputMode));
                """,
                new
                {
                    question.SessionId,
                    question.WordGroupId,
                    question.MemberId,
                    question.FormCode,
                    question.OutputMode
                },
                transaction,
                cancellationToken: cancellationToken));
            if (targetCount != 1)
            {
                throw InvalidData("题目目标不属于当前会话或没有对应输出词形。");
            }
        }

        private static Task<long> InsertQuestionAsync(
            SqliteConnection connection,
            System.Data.Common.DbTransaction transaction,
            CreateLearningQuestionPo question,
            CancellationToken cancellationToken)
        {
            return connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO LearningQuestions (
                    SessionId, WordGroupId, MemberId, FormCode,
                    Channel, OutputMode, QuestionOrdinal, Status, CreatedAtUtc)
                VALUES (
                    @SessionId, @WordGroupId, @MemberId, @FormCode,
                    @Channel, @OutputMode, @QuestionOrdinal, 'Pending', @CreatedAtUtc);
                SELECT last_insert_rowid();
                """,
                new
                {
                    question.SessionId,
                    question.WordGroupId,
                    question.MemberId,
                    question.FormCode,
                    question.Channel,
                    question.OutputMode,
                    question.QuestionOrdinal,
                    CreatedAtUtc = FormatDateTime(question.CreatedAtUtc)
                },
                transaction,
                cancellationToken: cancellationToken));
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
            public string? ScriptCode { get; set; }
            public string? KanjiUsageCode { get; set; }
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
            public int HasCompletedProgress { get; set; }
        }

        private sealed class BrowseGroupRow
        {
            public int SortOrder { get; set; }
            public string WordGroupId { get; set; } = string.Empty;
        }

        private class QuestionRow
        {
            public long QuestionId { get; set; }
            public string SessionId { get; set; } = string.Empty;
            public string WordGroupId { get; set; } = string.Empty;
            public long MemberId { get; set; }
            public string FormCode { get; set; } = string.Empty;
            public string Channel { get; set; } = string.Empty;
            public string OutputMode { get; set; } = string.Empty;
            public int QuestionOrdinal { get; set; }
        }

        private sealed class FormUnitRow
        {
            public string WordGroupId { get; set; } = string.Empty;
            public long MemberId { get; set; }
            public string FormCode { get; set; } = string.Empty;
            public string FormLabel { get; set; } = string.Empty;
            public int IsPhrase { get; set; }
            public string? KanjiUsageCode { get; set; }
            public string MeaningLanguage { get; set; } = string.Empty;
            public int CorrectStreak { get; set; }
            public int IsMastered { get; set; }
            public int NextEligibleOrdinal { get; set; }
            public int MeaningWritingCount { get; set; }
            public int DictationCount { get; set; }
            public int LatinCount { get; set; }
            public int KanaCount { get; set; }
            public int KanjiCount { get; set; }
            public int CorrectCount { get; set; }
            public int ErrorCount { get; set; }
        }

        private sealed class AnswerVariantRow
        {
            public long MemberId { get; set; }
            public string FormCode { get; set; } = string.Empty;
            public string Surface { get; set; } = string.Empty;
            public string? Reading { get; set; }
            public string? ScriptCode { get; set; }
            public int IsCommon { get; set; }
            public int IsPrimary { get; set; }
            public int FormIndex { get; set; }
        }

        private sealed class QuestionHistoryRow
        {
            public long QuestionId { get; set; }
            public string WordGroupId { get; set; } = string.Empty;
            public long MemberId { get; set; }
            public string FormCode { get; set; } = string.Empty;
            public string Channel { get; set; } = string.Empty;
            public string OutputMode { get; set; } = string.Empty;
            public int QuestionOrdinal { get; set; }
            public int? WasCorrect { get; set; }
        }

        private sealed class SessionStateRow
        {
            public string Status { get; set; } = string.Empty;
            public int CurrentOrdinal { get; set; }
        }

        private sealed class AnswerTargetRow : QuestionRow
        {
            public string QuestionStatus { get; set; } = string.Empty;
            public string SessionStatus { get; set; } = string.Empty;
            public int CurrentOrdinal { get; set; }
        }
    }
}
