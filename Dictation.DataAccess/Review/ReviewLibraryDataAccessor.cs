using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Runtime;
using Dapper;

namespace Dictation.DataAccess.Review
{
    /// <summary>只读取用户复习词组及已有记忆快照，不计算风险或推进进度。</summary>
    public sealed class ReviewLibraryDataAccessor
    {
        private readonly SqliteConnectionFactory connectionFactory;
        private readonly WordGroupMemberSnapshotReader memberReader;

        /// <summary>组合用户连接和共享成员快照读取能力。</summary>
        /// <param name="connectionFactory">创建用户数据库连接。</param>
        /// <param name="memberReader">读取完整词组成员及词形。</param>
        internal ReviewLibraryDataAccessor(SqliteConnectionFactory connectionFactory, WordGroupMemberSnapshotReader memberReader)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.memberReader = memberReader ?? throw new ArgumentNullException(nameof(memberReader));
        }

        /// <summary>读取指定语言的复习区域；没有状态的旧词仍返回，不补造学习历史。</summary>
        /// <param name="language">Japanese 或 English 的持久化语言代码。</param>
        /// <param name="cancellationToken">取消数据库查询的令牌。</param>
        public async Task<ReviewLibrarySnapshotPo> LoadAsync(string language, CancellationToken cancellationToken = default)
        {
            if (language is not ("Japanese" or "English"))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "复习词库语言不受支持。");
            }
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            // 延迟只读事务保证组、成员、记忆来自同一快照，不提前取得写锁。
            await using var transaction = connection.BeginTransaction(deferred: true);
            var rows = (await connection.QueryAsync<ReviewLibraryGroupRow>(new CommandDefinition(
                """
                SELECT WordGroupId, Language, AnchorSourceSenseId, Placement, CreatedAtUtc
                FROM WordGroups WHERE Language = @Language AND Placement = 'Review'
                ORDER BY CreatedAtUtc, WordGroupId;
                """, new { Language = language }, transaction, cancellationToken: cancellationToken))).ToArray();
            var members = await memberReader.ReadAsync(connection, transaction,
                rows.Select(row => row.WordGroupId).ToArray(), cancellationToken);
            var memories = (await connection.QueryAsync<ReviewStoredMemoryPo>(new CommandDefinition(
                """
                SELECT Memories.WordGroupId, Memories.MemberId, Members.SourceSenseId, Memories.FormCode,
                       Memories.Stability, Memories.Difficulty, Memories.LastReviewedAtUtc,
                       Memories.DueAtUtc, Memories.ReviewCount, Memories.LapseCount
                FROM ReviewFormMemories AS Memories
                INNER JOIN WordGroupMembers AS Members ON Members.MemberId = Memories.MemberId
                    AND Members.WordGroupId = Memories.WordGroupId
                INNER JOIN WordGroups AS Groups ON Groups.WordGroupId = Memories.WordGroupId
                WHERE Groups.Language = @Language AND Groups.Placement = 'Review';
                """, new { Language = language }, transaction, cancellationToken: cancellationToken))).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken);
            return new ReviewLibrarySnapshotPo(ReviewLibrarySnapshotMapper.ToGroups(rows, members), memories);
        }
    }
}
