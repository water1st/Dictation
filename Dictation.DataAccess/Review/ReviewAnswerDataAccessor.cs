using Dapper;
using Dictation.DataAccess.Practice;
using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Review
{
    /// <summary>在单一 SQLite 写事务内提交复习作答、独立目标成绩、记忆和下一题。</summary>
    public sealed class ReviewAnswerDataAccessor
    {
        private readonly SqliteConnectionFactory connectionFactory;
        private readonly ReviewQuestionWriter questionWriter;
        private readonly ReviewMemoryWriter memoryWriter;

        /// <summary>组合连接工厂与复用调用方事务的题目、记忆写入组件。</summary>
        /// <param name="connectionFactory">按操作创建用户连接。</param>
        /// <param name="questionWriter">验证并保存下一题。</param>
        /// <param name="memoryWriter">保存业务层已计算的记忆。</param>
        internal ReviewAnswerDataAccessor(SqliteConnectionFactory connectionFactory, ReviewQuestionWriter questionWriter, ReviewMemoryWriter memoryWriter)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.questionWriter = questionWriter ?? throw new ArgumentNullException(nameof(questionWriter));
            this.memoryWriter = memoryWriter ?? throw new ArgumentNullException(nameof(memoryWriter));
        }

        /// <summary>复查待答题和归属后写入全部结果；任何失败由事务释放整体回滚。</summary>
        /// <param name="answer">业务层完成客观评价及调度的提交输入。</param>
        /// <param name="cancellationToken">取消整个成绩提交。</param>
        /// <returns>共同提交的下一题；完成会话时为空。</returns>
        public async Task<PracticeQuestionStatePo?> ApplyAsync(ApplyReviewAnswerPo answer, CancellationToken cancellationToken = default)
        {
            Validate(answer);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            var pending = await connection.QuerySingleOrDefaultAsync<ReviewQuestionRow>(new CommandDefinition("""
                SELECT Questions.QuestionId, Questions.SessionId, Questions.WordGroupId, Questions.MemberId,
                    Questions.FormCode, Questions.Channel, Questions.OutputMode, Questions.QuestionOrdinal
                FROM ReviewQuestions AS Questions INNER JOIN ReviewSessions AS Sessions ON Sessions.SessionId=Questions.SessionId
                WHERE Questions.SessionId=@SessionId AND Questions.QuestionId=@QuestionId AND Questions.Status='Pending'
                    AND Sessions.Status='InProgress' AND Questions.QuestionOrdinal=Sessions.CurrentOrdinal;
                """, answer, transaction, cancellationToken: cancellationToken));
            if (pending is null)
            {
                throw new InvalidOperationException("复习题目已经过期、删除或完成，未提交任何成绩。");
            }
            if (!answer.Outcomes.Any(value => value.MemberId == pending.MemberId && value.FormCode == pending.FormCode && value.WordGroupId == pending.WordGroupId))
            {
                throw new ArgumentException("提交成绩缺少当前题目的实际目标。", nameof(answer));
            }
            var answeredAt = answer.AnsweredAtUtc.ToUniversalTime().ToString("O");
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE ReviewQuestions SET Status='Answered', SubmittedText=@SubmittedText, WasCorrect=@WasCorrect,
                    AnsweredAtUtc=@AnsweredAtUtc WHERE QuestionId=@QuestionId;
                """, new { answer.QuestionId, answer.SubmittedText, WasCorrect = answer.WasCorrect ? 1 : 0, AnsweredAtUtc = answeredAt },
                transaction, cancellationToken: cancellationToken));
            foreach (var outcome in answer.Outcomes)
            {
                var updated = await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE ReviewSessionForms SET CorrectStreak=@CorrectStreak, IsMastered=@IsMastered,
                        NextEligibleOrdinal=@NextEligibleOrdinal, MeaningWritingCount=@MeaningWritingCount, DictationCount=@DictationCount,
                        LatinCount=@LatinCount, KanaCount=@KanaCount, KanjiCount=@KanjiCount, CorrectCount=@CorrectCount, ErrorCount=@ErrorCount
                    WHERE SessionId=@SessionId AND WordGroupId=@WordGroupId AND MemberId=@MemberId AND FormCode=@FormCode AND IsMastered=0;
                    """, new { answer.SessionId, outcome.WordGroupId, outcome.MemberId, outcome.FormCode, outcome.CorrectStreak,
                        IsMastered = outcome.IsMastered ? 1 : 0, outcome.NextEligibleOrdinal, outcome.MeaningWritingCount, outcome.DictationCount,
                        outcome.LatinCount, outcome.KanaCount, outcome.KanjiCount, outcome.CorrectCount, outcome.ErrorCount },
                    transaction, cancellationToken: cancellationToken));
                if (updated != 1)
                {
                    throw new InvalidOperationException("作答目标已删除或不属于当前未完成形式。");
                }
                // 每个共享听写目标保存自己的观察；删除主出题组不级联另一组的历史。
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO ReviewAnswerAttempts (SessionId, QuestionId, QuestionOrdinal, WordGroupId, MemberId, FormCode,
                        Channel, OutputMode, SubmittedText, WasCorrect, AnsweredAtUtc)
                    VALUES (@SessionId, @QuestionId, @QuestionOrdinal, @WordGroupId, @MemberId, @FormCode,
                        @Channel, @OutputMode, @SubmittedText, @WasCorrect, @AnsweredAtUtc);
                    """, new { answer.SessionId, answer.QuestionId, pending.QuestionOrdinal, outcome.WordGroupId, outcome.MemberId,
                        outcome.FormCode, pending.Channel, pending.OutputMode, answer.SubmittedText,
                        WasCorrect = answer.WasCorrect ? 1 : 0, AnsweredAtUtc = answeredAt }, transaction, cancellationToken: cancellationToken));
            }
            var nextOrdinal = pending.QuestionOrdinal + 1;
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE ReviewSessions SET CurrentOrdinal=@NextOrdinal WHERE SessionId=@SessionId;",
                new { answer.SessionId, NextOrdinal = nextOrdinal }, transaction, cancellationToken: cancellationToken));
            foreach (var groupId in answer.CompletedWordGroupIds)
            {
                var updated = await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE ReviewSessionGroups SET CompletedLocalDate=@LocalDate
                    WHERE SessionId=@SessionId AND WordGroupId=@GroupId AND CompletedLocalDate IS NULL
                        AND EXISTS (SELECT 1 FROM ReviewSessionForms WHERE SessionId=@SessionId AND WordGroupId=@GroupId)
                        AND NOT EXISTS (SELECT 1 FROM ReviewSessionForms WHERE SessionId=@SessionId AND WordGroupId=@GroupId AND IsMastered=0);
                    """, new { answer.SessionId, GroupId = groupId, LocalDate = answer.AnsweredLocalDate },
                    transaction, cancellationToken: cancellationToken));
                if (updated != 1)
                {
                    throw new InvalidOperationException("待完成词组不属于本轮，或仍有未完成形式。");
                }
            }
            await memoryWriter.UpsertAsync(connection, transaction, answer.Memories, cancellationToken);
            PracticeQuestionStatePo? next = null;
            if (answer.CompleteSession)
            {
                var updated = await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE ReviewSessions SET Status='Completed', CompletedAtUtc=@Now
                    WHERE SessionId=@SessionId AND NOT EXISTS (SELECT 1 FROM ReviewSessionForms WHERE SessionId=@SessionId AND IsMastered=0);
                    """, new { answer.SessionId, Now = answeredAt }, transaction, cancellationToken: cancellationToken));
                if (updated != 1)
                {
                    throw new InvalidOperationException("复习会话仍有未完成形式，不能提交完成状态。");
                }
            }
            else
            {
                var scheduled = answer.NextQuestion ?? throw new ArgumentException("未完成会话必须有下一题。", nameof(answer));
                if (scheduled.SessionId != answer.SessionId || scheduled.QuestionOrdinal != nextOrdinal)
                {
                    throw new ArgumentException("下一题所属会话或序号不正确。", nameof(answer));
                }
                next = await questionWriter.InsertAsync(connection, transaction, scheduled, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return next;
        }

        /// <summary>验证提交结构及记忆归属；当前题目和数据库状态在写事务内复查。</summary>
        /// <param name="answer">待提交的结果。</param>
        private static void Validate(ApplyReviewAnswerPo answer)
        {
            ArgumentNullException.ThrowIfNull(answer);
            ArgumentException.ThrowIfNullOrWhiteSpace(answer.SessionId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(answer.QuestionId);
            ArgumentNullException.ThrowIfNull(answer.SubmittedText);
            ArgumentNullException.ThrowIfNull(answer.Outcomes);
            ArgumentNullException.ThrowIfNull(answer.CompletedWordGroupIds);
            ArgumentNullException.ThrowIfNull(answer.Memories);
            if (answer.Outcomes.Count == 0 || answer.Outcomes.DistinctBy(value => (value.MemberId, value.FormCode)).Count() != answer.Outcomes.Count)
            {
                throw new ArgumentException("提交必须包含不重复的目标成绩。", nameof(answer));
            }
            var completed = answer.Outcomes.Where(value => value.IsMastered).Select(value => (value.MemberId, value.FormCode)).ToHashSet();
            if (!completed.SetEquals(answer.Memories.Select(value => (value.MemberId, value.FormCode)))
                || answer.Memories.Count != completed.Count || (answer.CompleteSession && answer.NextQuestion is not null))
            {
                throw new ArgumentException("完成形式、记忆更新或下一题不一致。", nameof(answer));
            }
        }
    }
}
