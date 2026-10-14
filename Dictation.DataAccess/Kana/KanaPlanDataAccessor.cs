using Dapper;
using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Kana
{
    /// <summary>只读写单份五十音计划的SQL，不负责范围规则或默认业务值。</summary>
    public sealed class KanaPlanDataAccessor
    {
        private readonly SqliteConnectionFactory connections;
        /// <summary>组合用户数据库连接工厂。</summary>
        /// <param name="connections">提供独立可释放连接。</param>
        public KanaPlanDataAccessor(SqliteConnectionFactory connections) => this.connections = connections ?? throw new ArgumentNullException(nameof(connections));

        /// <summary>读取保存的数据库列，未保存时为空。</summary>
        /// <param name="cancellationToken">数据库取消令牌。</param>
        /// <returns>持久化计划或空。</returns>
        public async Task<KanaPlanPo?> LoadAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<KanaPlanPo>(new CommandDefinition("SELECT * FROM KanaPlans WHERE Id=1;", cancellationToken: cancellationToken));
        }

        /// <summary>一次语句原子写入完整计划列。</summary>
        /// <param name="plan">业务层已经校验的列值。</param>
        /// <param name="cancellationToken">数据库取消令牌。</param>
        public async Task SaveAsync(KanaPlanPo plan, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plan);
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO KanaPlans (Id,Enabled,Hiragana,Katakana,Clear,Voiced,Contracted,ReviewAll,DailyTarget)
                VALUES (1,@Enabled,@Hiragana,@Katakana,@Clear,@Voiced,@Contracted,@ReviewAll,@DailyTarget)
                ON CONFLICT(Id) DO UPDATE SET Enabled=excluded.Enabled,Hiragana=excluded.Hiragana,
                Katakana=excluded.Katakana,Clear=excluded.Clear,Voiced=excluded.Voiced,
                Contracted=excluded.Contracted,ReviewAll=excluded.ReviewAll,DailyTarget=excluded.DailyTarget;
                """, plan, cancellationToken: cancellationToken));
        }
    }
}
