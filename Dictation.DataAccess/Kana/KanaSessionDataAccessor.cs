using Dapper;
using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Kana
{
    /// <summary>负责五十音轮次、观察和独立记忆CRUD以及原子乐观锁，不编排业务。</summary>
    public sealed class KanaSessionDataAccessor
    {
        private readonly SqliteConnectionFactory connections;
        /// <summary>组合用户数据库连接工厂，不引用词库连接。</summary>
        /// <param name="connections">提供独立可释放连接。</param>
        public KanaSessionDataAccessor(SqliteConnectionFactory connections) => this.connections = connections ?? throw new ArgumentNullException(nameof(connections));

        /// <summary>读取唯一活动轮次。</summary>
        /// <param name="cancellationToken">取消读取令牌。</param>
        /// <returns>活动轮次，没有时为空。</returns>
        public async Task<KanaSessionPo?> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<KanaSessionPo>(new CommandDefinition("SELECT * FROM KanaSessions WHERE Status='Active';", cancellationToken: cancellationToken));
        }

        /// <summary>读取指定轮次，包括已经结束的轮次。</summary>
        /// <param name="sessionId">轮次主键。</param>
        /// <param name="cancellationToken">取消读取令牌。</param>
        /// <returns>轮次，不存在时为空。</returns>
        public async Task<KanaSessionPo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<KanaSessionPo>(new CommandDefinition("SELECT * FROM KanaSessions WHERE SessionId=@sessionId;", new { sessionId }, cancellationToken: cancellationToken));
        }

        /// <summary>按原始随机队列读取项目快照及重试状态。</summary>
        /// <param name="sessionId">轮次主键。</param>
        /// <param name="cancellationToken">取消读取令牌。</param>
        /// <returns>该轮次的独立项目。</returns>
        public async Task<IReadOnlyList<KanaSessionItemPo>> GetItemsAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return (await connection.QueryAsync<KanaSessionItemPo>(new CommandDefinition("SELECT * FROM KanaSessionItems WHERE SessionId=@sessionId ORDER BY QueueOrder;", new { sessionId }, cancellationToken: cancellationToken))).ToArray();
        }

        /// <summary>读取所有独立记忆列，范围最多214项。</summary>
        /// <param name="cancellationToken">取消读取令牌。</param>
        /// <returns>已评价的五十音记忆，不读取词汇记忆。</returns>
        public async Task<IReadOnlyList<KanaMemoryPo>> GetMemoriesAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return (await connection.QueryAsync<KanaMemoryPo>(new CommandDefinition("SELECT * FROM KanaMemories;", cancellationToken: cancellationToken))).ToArray();
        }

        /// <summary>读取在实际本地完成日期完成过的独立项目标识。</summary>
        /// <param name="localDate">统一时钟给出的yyyy-MM-dd。</param>
        /// <param name="cancellationToken">取消读取令牌。</param>
        /// <returns>去重标识，重复重练不重复计数。</returns>
        public async Task<IReadOnlyList<string>> GetCompletedIdsAsync(string localDate, CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return (await connection.QueryAsync<string>(new CommandDefinition("SELECT DISTINCT KanaId FROM KanaSessionItems WHERE IsCompleted=1 AND CompletedLocalDate=@localDate;", new { localDate }, cancellationToken: cancellationToken))).ToArray();
        }

        /// <summary>在事务内重新检查活动轮次，存在则恢复，不存在才保存完整队列。</summary>
        /// <param name="session">业务层创建的轮次列。</param>
        /// <param name="items">该轮次所有项目快照。</param>
        /// <param name="cancellationToken">取消事务令牌。</param>
        /// <returns>本次创建或竞争先创建的活动轮次。</returns>
        public async Task<KanaSessionPo> CreateAsync(KanaSessionPo session, IReadOnlyList<KanaSessionItemPo> items, CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();
            var active = await connection.QuerySingleOrDefaultAsync<KanaSessionPo>(new CommandDefinition("SELECT * FROM KanaSessions WHERE Status='Active';", transaction: transaction, cancellationToken: cancellationToken));
            if (active is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return active;
            }
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO KanaSessions(SessionId,StartedLocalDate,Status,QuestionOrdinal,CurrentKanaId)
                VALUES(@SessionId,@StartedLocalDate,@Status,@QuestionOrdinal,@CurrentKanaId);
                """, session, transaction, cancellationToken: cancellationToken));
            foreach (var item in items)
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO KanaSessionItems(SessionId,KanaId,Text,Reading,RowHint,Script,Range,QueueOrder)
                    VALUES(@SessionId,@KanaId,@Text,@Reading,@RowHint,@Script,@Range,@QueueOrder);
                    """, item, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return session;
        }

        /// <summary>用题序和项目乐观锁提交一次观察、状态及可选记忆；竞争失败全部回滚。</summary>
        /// <param name="sessionId">活动轮次标识。</param>
        /// <param name="expectedOrdinal">作答前题序。</param>
        /// <param name="item">业务层计算后的项目状态。</param>
        /// <param name="attempt">原始作答观察。</param>
        /// <param name="nextKanaId">下一项目；为空则结束轮次。</param>
        /// <param name="memory">仅项目完成时写入一次记忆，未完成为空。</param>
        /// <param name="cancellationToken">取消整笔事务令牌。</param>
        public async Task CommitAsync(string sessionId, int expectedOrdinal, KanaSessionItemPo item, KanaAttemptPo attempt,
            string? nextKanaId, KanaMemoryPo? memory, CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();
            var changed = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE KanaSessions SET QuestionOrdinal=QuestionOrdinal+1, CurrentKanaId=@nextKanaId,
                    Status=CASE WHEN @nextKanaId IS NULL THEN 'Completed' ELSE 'Active' END
                WHERE SessionId=@sessionId AND Status='Active' AND QuestionOrdinal=@expectedOrdinal AND CurrentKanaId=@KanaId;
                """, new { sessionId, expectedOrdinal, nextKanaId, item.KanaId }, transaction, cancellationToken: cancellationToken));
            if (changed != 1) throw new InvalidOperationException("题目已更新，请读取当前题后再作答。");
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO KanaAttempts(SessionId,QuestionOrdinal,KanaId,SubmittedText,WasCorrect,AnsweredAtUtc)
                VALUES(@SessionId,@QuestionOrdinal,@KanaId,@SubmittedText,@WasCorrect,@AnsweredAtUtc);
                """, attempt, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE KanaSessionItems SET HasWrong=@HasWrong,CorrectStreak=@CorrectStreak,
                NextEligibleOrdinal=@NextEligibleOrdinal,IsCompleted=@IsCompleted,CompletedLocalDate=@CompletedLocalDate
                WHERE SessionId=@SessionId AND KanaId=@KanaId;
                """, item, transaction, cancellationToken: cancellationToken));
            if (memory is not null)
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO KanaMemories(KanaId,Stability,Difficulty,LastReviewedAtUtc,DueAtUtc,ReviewCount,LapseCount)
                    VALUES(@KanaId,@Stability,@Difficulty,@LastReviewedAtUtc,@DueAtUtc,@ReviewCount,@LapseCount)
                    ON CONFLICT(KanaId) DO UPDATE SET Stability=excluded.Stability,Difficulty=excluded.Difficulty,
                    LastReviewedAtUtc=excluded.LastReviewedAtUtc,DueAtUtc=excluded.DueAtUtc,
                    ReviewCount=excluded.ReviewCount,LapseCount=excluded.LapseCount;
                    """, memory, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }

        /// <summary>读取原题序已保存的观察及正确答案，不写入任何状态。</summary>
        /// <param name="sessionId">轮次标识。</param>
        /// <param name="questionOrdinal">需要确认的原提交题序。</param>
        /// <param name="cancellationToken">取消只读查询。</param>
        /// <returns>已保存观察及原正确答案，未保存时为空。</returns>
        public async Task<KanaAttemptPo?> GetAttemptAsync(string sessionId, int questionOrdinal, CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<KanaAttemptPo>(new CommandDefinition("""
                SELECT A.*, I.Text AS CorrectAnswer FROM KanaAttempts A
                JOIN KanaSessionItems I ON I.SessionId=A.SessionId AND I.KanaId=A.KanaId
                WHERE A.SessionId=@sessionId AND A.QuestionOrdinal=@questionOrdinal;
                """, new { sessionId, questionOrdinal }, cancellationToken: cancellationToken));
        }

        /// <summary>读取保留的原始错误，正确重试不删除旧观察。</summary>
        /// <param name="sessionId">轮次标识。</param>
        /// <param name="cancellationToken">取消读取令牌。</param>
        /// <returns>按作答题序排列的错误观察及当时正确答案。</returns>
        public async Task<IReadOnlyList<KanaAttemptPo>> GetErrorsAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return (await connection.QueryAsync<KanaAttemptPo>(new CommandDefinition("""
                SELECT A.*, I.Text AS CorrectAnswer FROM KanaAttempts A
                JOIN KanaSessionItems I ON I.SessionId=A.SessionId AND I.KanaId=A.KanaId
                WHERE A.SessionId=@sessionId AND A.WasCorrect=0 ORDER BY A.QuestionOrdinal;
                """, new { sessionId }, cancellationToken: cancellationToken))).ToArray();
        }
    }
}
