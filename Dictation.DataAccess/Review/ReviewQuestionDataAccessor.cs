using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Practice;
using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Review
{
    /// <summary>读取独立复习调度快照并原子创建待答题，不执行出题算法或判题。</summary>
    public sealed class ReviewQuestionDataAccessor
    {
        private readonly SqliteConnectionFactory connectionFactory;

        /// <summary>组合唯一用户数据库的连接工厂。</summary>
        /// <param name="connectionFactory">创建按操作释放的 SQLite 连接。</param>
        public ReviewQuestionDataAccessor(SqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        /// <summary>在同一读事务内读取复习主表、形式、待答题与历史。</summary>
        /// <param name="sessionId">复习会话标识。</param>
        /// <param name="cancellationToken">取消本次快照读取。</param>
        public async Task<ReviewQuestionSnapshotPo> GetSnapshotAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction(deferred: true);
            var arguments = new { SessionId = sessionId };
            var header = await connection.QuerySingleOrDefaultAsync<ReviewSessionHeaderRow>(new CommandDefinition(
                "SELECT SessionId, Language, Status, CurrentOrdinal, PlanJson, CreatedAtUtc FROM ReviewSessions WHERE SessionId=@SessionId;",
                arguments, transaction, cancellationToken: cancellationToken)) ?? throw new KeyNotFoundException("复习会话不存在。");
            var groups = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT WordGroupId FROM ReviewSessionGroups WHERE SessionId=@SessionId ORDER BY SortOrder;",
                arguments, transaction, cancellationToken: cancellationToken))).ToArray();
            var rows = (await connection.QueryAsync<ReviewPracticeFormRow>(new CommandDefinition("""
                SELECT States.WordGroupId, States.MemberId, States.FormCode,
                    COALESCE((SELECT FormLabel FROM WordGroupForms WHERE MemberId=States.MemberId AND FormCode=States.FormCode
                        ORDER BY IsPrimary DESC, IsCommon DESC, FormIndex LIMIT 1), States.FormCode) AS FormLabel,
                    Members.IsPhrase, Members.KanjiUsageCode, Members.MeaningLanguage,
                    States.CorrectStreak, States.IsMastered, States.NextEligibleOrdinal,
                    States.MeaningWritingCount, States.DictationCount, States.LatinCount, States.KanaCount, States.KanjiCount,
                    States.CorrectCount, States.ErrorCount
                FROM ReviewSessionForms AS States
                INNER JOIN WordGroupMembers AS Members ON Members.MemberId=States.MemberId
                INNER JOIN ReviewSessionGroups AS Groups ON Groups.SessionId=States.SessionId AND Groups.WordGroupId=States.WordGroupId
                WHERE States.SessionId=@SessionId ORDER BY Groups.SortOrder, Members.SortOrder, States.FormCode;
                """, arguments, transaction, cancellationToken: cancellationToken))).ToArray();
            var memberArguments = new { MemberIds = rows.Select(row => row.MemberId).Distinct().ToArray() };
            var meanings = (await connection.QueryAsync<WordGroupMeaningRow>(new CommandDefinition(
                "SELECT MemberId, MeaningIndex, Text FROM WordGroupMeanings WHERE MemberId IN @MemberIds ORDER BY MemberId, MeaningIndex;",
                memberArguments, transaction, cancellationToken: cancellationToken))).ToArray();
            var variants = (await connection.QueryAsync<WordGroupFormRow>(new CommandDefinition("""
                SELECT MemberId, FormIndex, Surface, Reading, FormCode, ScriptCode, RoleCode, FormLabel, DetailLabel, IsCommon, IsPrimary
                FROM WordGroupForms WHERE MemberId IN @MemberIds ORDER BY MemberId, FormCode, IsPrimary DESC, IsCommon DESC, FormIndex;
                """, memberArguments, transaction, cancellationToken: cancellationToken))).ToArray();
            var questions = (await connection.QueryAsync<ReviewQuestionRow>(new CommandDefinition(
                "SELECT QuestionId, SessionId, WordGroupId, MemberId, FormCode, Channel, OutputMode, QuestionOrdinal, Status, WasCorrect FROM ReviewQuestions WHERE SessionId=@SessionId ORDER BY QuestionOrdinal;",
                arguments, transaction, cancellationToken: cancellationToken))).ToArray();
            await transaction.CommitAsync(cancellationToken);
            return ReviewQuestionSnapshotMapper.ToPo(header, groups, rows, meanings, variants, questions);
        }

        /// <summary>在写事务内复查状态和归属；并发创建返回已持久化的当前待答题。</summary>
        /// <param name="question">业务层已经完成纯调度的待答题。</param>
        /// <param name="cancellationToken">取消本次创建事务。</param>
        public async Task<PracticeQuestionStatePo> CreatePendingAsync(CreatePracticeQuestionPo question, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(question);
            ArgumentException.ThrowIfNullOrWhiteSpace(question.SessionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(question.WordGroupId);
            ArgumentException.ThrowIfNullOrWhiteSpace(question.FormCode);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(question.MemberId);
            ArgumentOutOfRangeException.ThrowIfNegative(question.QuestionOrdinal);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            var valid = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM ReviewSessions WHERE SessionId=@SessionId AND Status='InProgress' AND CurrentOrdinal=@QuestionOrdinal);",
                question, transaction, cancellationToken: cancellationToken));
            if (!valid)
            {
                throw new InvalidOperationException("复习会话已结束或题号已经过期。");
            }
            var pending = await connection.QuerySingleOrDefaultAsync<ReviewQuestionRow>(new CommandDefinition(
                "SELECT QuestionId, SessionId, WordGroupId, MemberId, FormCode, Channel, OutputMode, QuestionOrdinal FROM ReviewQuestions WHERE SessionId=@SessionId AND Status='Pending';",
                question, transaction, cancellationToken: cancellationToken));
            if (pending is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return ReviewQuestionSnapshotMapper.ToQuestion(pending);
            }
            var target = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT EXISTS (SELECT 1 FROM ReviewSessionForms AS States
                    INNER JOIN ReviewSessions AS Sessions ON Sessions.SessionId=States.SessionId
                    WHERE States.SessionId=@SessionId AND States.WordGroupId=@WordGroupId AND States.MemberId=@MemberId
                        AND States.FormCode=@FormCode AND States.IsMastered=0
                        AND ((Sessions.Language='English' AND @OutputMode='Latin')
                            OR (Sessions.Language='Japanese' AND @OutputMode IN ('Kana','Kanji')))
                        AND EXISTS (SELECT 1 FROM WordGroupForms WHERE MemberId=States.MemberId AND FormCode=States.FormCode
                            AND (@OutputMode='Latin' OR ScriptCode=@OutputMode)));
                """, question, transaction, cancellationToken: cancellationToken));
            if (!target)
            {
                throw new InvalidOperationException("复习题目不属于本轮适用形式，或对应词组已经删除。");
            }
            var questionId = await connection.ExecuteScalarAsync<long>(new CommandDefinition("""
                INSERT INTO ReviewQuestions (SessionId, WordGroupId, MemberId, FormCode, Channel, OutputMode, QuestionOrdinal, Status, CreatedAtUtc)
                VALUES (@SessionId, @WordGroupId, @MemberId, @FormCode, @Channel, @OutputMode, @QuestionOrdinal, 'Pending', @CreatedAtUtc);
                SELECT last_insert_rowid();
                """, new { question.SessionId, question.WordGroupId, question.MemberId, question.FormCode, question.Channel,
                    question.OutputMode, question.QuestionOrdinal, CreatedAtUtc = question.CreatedAtUtc.ToUniversalTime().ToString("O") },
                transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new PracticeQuestionStatePo(questionId, question.SessionId, question.WordGroupId, question.MemberId,
                question.FormCode, question.Channel, question.OutputMode, question.QuestionOrdinal);
        }
    }
}
