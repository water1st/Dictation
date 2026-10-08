using System.Data.Common;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Review
{
    /// <summary>使用调用方已有事务保存成员词形记忆，不创建连接或计算评价。</summary>
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
            await WriteAsync(connection, transaction, seeds, overwriteExisting: false, cancellationToken);
        }

        /// <summary>保存本次复习完成形式的新记忆，同调用方成绩共同提交或回滚。</summary>
        /// <param name="connection">调用方已经打开的用户连接。</param>
        /// <param name="transaction">调用方负责的成绩提交事务。</param>
        /// <param name="updates">待插入或更新的已评价记忆。</param>
        /// <param name="cancellationToken">取消数据库操作。</param>
        internal async Task UpsertAsync(SqliteConnection connection, DbTransaction transaction,
            IReadOnlyList<ReviewFormMemoryPo> updates, CancellationToken cancellationToken)
        {
            await WriteAsync(connection, transaction, updates, overwriteExisting: true, cancellationToken);
        }

        /// <summary>统一验证成员归属并复用参数转换，两种保存策略只改变冲突处理。</summary>
        /// <param name="connection">调用方连接。</param>
        /// <param name="transaction">调用方事务。</param>
        /// <param name="seeds">准备保存的记忆。</param>
        /// <param name="overwriteExisting">复习覆盖旧状态；首次学习保留已有状态。</param>
        /// <param name="cancellationToken">取消数据库操作。</param>
        private static async Task WriteAsync(SqliteConnection connection, DbTransaction transaction,
            IReadOnlyList<ReviewFormMemoryPo> seeds, bool overwriteExisting, CancellationToken cancellationToken)
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

                // 冲突子句只来自内部固定策略，不接受用户 SQL；学习不能重置已有复习间隔。
                var onConflict = overwriteExisting ? """
                    DO UPDATE SET Stability=excluded.Stability, Difficulty=excluded.Difficulty,
                        LastReviewedAtUtc=excluded.LastReviewedAtUtc, DueAtUtc=excluded.DueAtUtc,
                        ReviewCount=excluded.ReviewCount, LapseCount=excluded.LapseCount
                    """ : "DO NOTHING";
                await connection.ExecuteAsync(new CommandDefinition(
                    $"""
                    INSERT INTO ReviewFormMemories (WordGroupId, MemberId, FormCode, Stability, Difficulty,
                        LastReviewedAtUtc, DueAtUtc, ReviewCount, LapseCount)
                    VALUES (@WordGroupId, @MemberId, @FormCode, @Stability, @Difficulty,
                        @LastReviewedAtUtc, @DueAtUtc, @ReviewCount, @LapseCount)
                    ON CONFLICT (MemberId, FormCode) {onConflict};
                    """, ReviewMemoryParameterMapper.ToParameters(seed), transaction, cancellationToken: cancellationToken));
            }
        }
    }
}
