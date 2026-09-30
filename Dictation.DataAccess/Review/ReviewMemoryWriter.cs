using System.Data.Common;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Review
{
    /// <summary>使用调用方已有事务写入尚不存在的成员词形记忆，不创建连接或计算评价。</summary>
    internal sealed class ReviewMemoryWriter
    {
        /// <summary>保存首次记忆，重复初始化必须保留以前的状态。</summary>
        /// <param name="connection">调用方已打开的用户数据库连接。</param>
        /// <param name="transaction">答题状态共同提交的事务。</param>
        /// <param name="seeds">待保存的成员词形记忆。</param>
        /// <param name="cancellationToken">取消数据库操作的令牌。</param>
        internal async Task InsertMissingAsync(SqliteConnection connection, DbTransaction transaction,
            IReadOnlyList<ReviewFormMemoryPo> seeds, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(seeds);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var seed in seeds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    """
                    SELECT EXISTS (
                        SELECT 1 FROM WordGroupMembers AS Members
                        INNER JOIN WordGroupForms AS Forms ON Forms.MemberId = Members.MemberId
                        WHERE Members.WordGroupId = @WordGroupId AND Members.MemberId = @MemberId
                          AND Forms.FormCode = @FormCode);
                    """, new { seed.WordGroupId, seed.MemberId, seed.FormCode }, transaction,
                    cancellationToken: cancellationToken));
                if (!exists)
                {
                    throw new InvalidDataException("记忆状态的成员或词形不属于指定词组。");
                }

                // 使用答题事务且仅插入缺失状态，补充学习不能重置已经形成的复习间隔。
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO ReviewFormMemories (WordGroupId, MemberId, FormCode, Stability, Difficulty,
                        LastReviewedAtUtc, DueAtUtc, ReviewCount, LapseCount)
                    VALUES (@WordGroupId, @MemberId, @FormCode, @Stability, @Difficulty,
                        @LastReviewedAtUtc, @DueAtUtc, @ReviewCount, @LapseCount)
                    ON CONFLICT (MemberId, FormCode) DO NOTHING;
                    """, new
                    {
                        seed.WordGroupId, seed.MemberId, seed.FormCode, seed.Stability, seed.Difficulty,
                        LastReviewedAtUtc = seed.LastReviewedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                        DueAtUtc = seed.DueAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                        seed.ReviewCount, seed.LapseCount
                    }, transaction, cancellationToken: cancellationToken));
            }
        }
    }
}
