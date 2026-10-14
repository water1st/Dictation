using System.Data.Common;
using Dapper;
using Dictation.DataAccess.Practice;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Review
{
    /// <summary>复用调用方的写事务验证归属并保存复习题目，不管理连接、事务或调度算法。</summary>
    internal sealed class ReviewQuestionWriter
    {
        /// <summary>在当前序号与实际目标词形仍有效时插入待答题。</summary>
        /// <param name="connection">调用方已经打开的用户连接。</param>
        /// <param name="transaction">调用方负责提交或回滚的事务。</param>
        /// <param name="question">已由业务层调度的下一题。</param>
        /// <param name="cancellationToken">取消数据库操作。</param>
        internal async Task<PracticeQuestionStatePo> InsertAsync(SqliteConnection connection, DbTransaction transaction,
            CreatePracticeQuestionPo question, CancellationToken cancellationToken)
        {
            var target = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT EXISTS (SELECT 1 FROM ReviewSessionForms AS States
                    INNER JOIN ReviewSessions AS Sessions ON Sessions.SessionId=States.SessionId
                    WHERE States.SessionId=@SessionId AND Sessions.Status='InProgress' AND Sessions.CurrentOrdinal=@QuestionOrdinal
                        AND States.WordGroupId=@WordGroupId AND States.MemberId=@MemberId
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
            return new PracticeQuestionStatePo(questionId, question.SessionId, question.WordGroupId, question.MemberId,
                question.FormCode, question.Channel, question.OutputMode, question.QuestionOrdinal);
        }
    }
}
