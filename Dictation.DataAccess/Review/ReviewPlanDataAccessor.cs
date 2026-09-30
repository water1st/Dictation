using Dictation.DataAccess.Runtime;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Dictation.DataAccess.Review
{
    /// <summary>在独立事务中读写复习计划、范围，并删除复习库整组。</summary>
    public sealed class ReviewPlanDataAccessor
    {
        private readonly SqliteConnectionFactory connectionFactory;

        /// <summary>注入用户数据库的连接工厂。</summary>
        /// <param name="connectionFactory">每次操作独立创建连接的工厂。</param>
        internal ReviewPlanDataAccessor(SqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        /// <summary>读取计划与有序选项，不存在时返回空。</summary>
        /// <param name="language">持久化语言代码。</param>
        /// <param name="cancellationToken">取消数据库读取的令牌。</param>
        /// <returns>持久化计划或空。</returns>
        public async Task<ReviewPlanPo?> GetPlanAsync(string language, CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            // 延迟只读事务保证主表与所有选项来自同一个快照，避免并发保存造成混合计划。
            using var transaction = connection.BeginTransaction(deferred: true);
            var row = await connection.QuerySingleOrDefaultAsync<PlanRow>(new CommandDefinition(
                "SELECT Language, DailyReviewTarget, SessionWordLimit, RangeMode, EnglishFormLabelMode, EnglishExampleSourceSenseId, UpdatedAtUtc FROM ReviewPlans WHERE Language = @Language;",
                new { Language = language }, transaction, cancellationToken: cancellationToken));
            if (row is null)
            {
                return null;
            }
            var groups = await ReadValuesAsync(connection, transaction, "ReviewPlanGroups", "WordGroupId", language, cancellationToken);
            var forms = await ReadValuesAsync(connection, transaction, "ReviewPlanForms", "FormCode", language, cancellationToken);
            var channels = await ReadValuesAsync(connection, transaction, "ReviewPlanChannels", "Channel", language, cancellationToken);
            var outputs = await ReadValuesAsync(connection, transaction, "ReviewPlanOutputs", "OutputMode", language, cancellationToken);
            return new ReviewPlanPo(row.Language, row.DailyReviewTarget, row.SessionWordLimit, row.RangeMode,
                groups, forms, channels, outputs, row.EnglishFormLabelMode, row.EnglishExampleSourceSenseId,
                DateTimeOffset.ParseExact(row.UpdatedAtUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }

        /// <summary>在同一事务中验证组归属并替换计划与选项。</summary>
        /// <param name="plan">待保存的独立复习计划。</param>
        /// <param name="cancellationToken">取消保存的令牌。</param>
        public async Task SavePlanAsync(ReviewPlanPo plan, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ValidateLanguage(plan.Language);
            ValidateValues(plan.FormCodes, nameof(plan.FormCodes), allowEmpty: false);
            ValidateValues(plan.Channels, nameof(plan.Channels), allowEmpty: false);
            ValidateValues(plan.OutputModes, nameof(plan.OutputModes), allowEmpty: false);
            ValidateValues(plan.WordGroupIds, nameof(plan.WordGroupIds), allowEmpty: plan.RangeMode == "All");
            if (plan.RangeMode == "All" && plan.WordGroupIds.Count != 0)
            {
                throw new ArgumentException("全部范围不能同时保存指定词组。", nameof(plan));
            }
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            if (plan.WordGroupIds.Count > 0)
            {
                var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(*) FROM WordGroups WHERE Language = @Language AND Placement = 'Review' AND WordGroupId IN @Ids;",
                    new { plan.Language, Ids = plan.WordGroupIds }, transaction, cancellationToken: cancellationToken));
                if (count != plan.WordGroupIds.Count)
                {
                    throw new ArgumentException("选择中包含不存在、其他语言或非复习库词组。", nameof(plan));
                }
            }
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO ReviewPlans (Language, DailyReviewTarget, SessionWordLimit, RangeMode,
                    EnglishFormLabelMode, EnglishExampleSourceSenseId, UpdatedAtUtc)
                VALUES (@Language, @DailyReviewTarget, @SessionWordLimit, @RangeMode,
                    @EnglishFormLabelMode, @EnglishExampleSourceSenseId, @UpdatedAtUtc)
                ON CONFLICT(Language) DO UPDATE SET
                    DailyReviewTarget = excluded.DailyReviewTarget, SessionWordLimit = excluded.SessionWordLimit,
                    RangeMode = excluded.RangeMode, EnglishFormLabelMode = excluded.EnglishFormLabelMode,
                    EnglishExampleSourceSenseId = excluded.EnglishExampleSourceSenseId, UpdatedAtUtc = excluded.UpdatedAtUtc;
                """, new
                {
                    plan.Language, plan.DailyReviewTarget, plan.SessionWordLimit, plan.RangeMode,
                    plan.EnglishFormLabelMode, plan.EnglishExampleSourceSenseId,
                    UpdatedAtUtc = plan.UpdatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                }, transaction, cancellationToken: cancellationToken));
            await ReplaceValuesAsync(connection, transaction, "ReviewPlanForms", "FormCode", plan.Language, plan.FormCodes, cancellationToken);
            await ReplaceValuesAsync(connection, transaction, "ReviewPlanChannels", "Channel", plan.Language, plan.Channels, cancellationToken);
            await ReplaceValuesAsync(connection, transaction, "ReviewPlanOutputs", "OutputMode", plan.Language, plan.OutputModes, cancellationToken);
            await ReplaceValuesAsync(connection, transaction, "ReviewPlanGroups", "WordGroupId", plan.Language, plan.WordGroupIds, cancellationToken);
            // 任意子表写入失败时由事务释放回滚，旧计划与全部范围不会被部分替换。
            await transaction.CommitAsync(cancellationToken);
        }

        /// <summary>仅删除当前语言 Review 整组，关联状态由外键级联。</summary>
        /// <param name="language">复习库语言代码。</param>
        /// <param name="wordGroupId">完整词组标识。</param>
        /// <param name="cancellationToken">取消删除的令牌。</param>
        public async Task DeleteWordGroupAsync(string language, string wordGroupId, CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            ArgumentException.ThrowIfNullOrWhiteSpace(wordGroupId);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var row = await connection.QuerySingleOrDefaultAsync<GroupStateRow>(new CommandDefinition(
                "SELECT Language, Placement FROM WordGroups WHERE WordGroupId = @Id;", new { Id = wordGroupId }, transaction, cancellationToken: cancellationToken));
            if (row is null)
            {
                throw new KeyNotFoundException("复习库词组不存在。");
            }
            if (row.Language != language || row.Placement != "Review")
            {
                throw new InvalidOperationException("只能删除当前语言复习库中的整组词条。");
            }
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM WordGroups WHERE WordGroupId = @Id;",
                new { Id = wordGroupId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }

        /// <summary>复用调用方快照读取已知选项子表，表名和列名仅由本类常量调用提供。</summary>
        /// <param name="connection">已打开的原数据库连接。</param>
        /// <param name="transaction">覆盖全部计划读取的快照事务。</param>
        /// <param name="table">本类指定的子表名称，不接受用户输入。</param>
        /// <param name="column">该子表的值列名。</param>
        /// <param name="language">读取的计划语言。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        /// <returns>按保存顺序排列的子表值。</returns>
        private static async Task<IReadOnlyList<string>> ReadValuesAsync(SqliteConnection connection, SqliteTransaction transaction,
            string table, string column, string language, CancellationToken cancellationToken) =>
            (await connection.QueryAsync<string>(new CommandDefinition(
                $"SELECT {column} FROM {table} WHERE Language = @Language ORDER BY SortOrder;",
                new { Language = language }, transaction, cancellationToken: cancellationToken))).ToArray();

        /// <summary>在原保存事务中替换一个有序子表；失败保持全部计划回滚。</summary>
        /// <param name="connection">原保存连接。</param>
        /// <param name="transaction">覆盖主表和全部子表的保存事务。</param>
        /// <param name="table">本类指定的子表名称，不接受用户输入。</param>
        /// <param name="column">该子表的值列名。</param>
        /// <param name="language">本次替换的计划语言。</param>
        /// <param name="values">已校验的有序选项。</param>
        /// <param name="cancellationToken">取消替换的令牌。</param>
        private static async Task ReplaceValuesAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction,
            string table, string column, string language, IReadOnlyList<string> values, CancellationToken cancellationToken)
        {
            await connection.ExecuteAsync(new CommandDefinition($"DELETE FROM {table} WHERE Language = @Language;",
                new { Language = language }, transaction, cancellationToken: cancellationToken));
            for (var index = 0; index < values.Count; index++)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    $"INSERT INTO {table} (Language, SortOrder, {column}) VALUES (@Language, @SortOrder, @Value);",
                    new { Language = language, SortOrder = index, Value = values[index] }, transaction, cancellationToken: cancellationToken));
            }
        }

        /// <summary>在进入事务前拒绝空、空白或重复的子表值，避免无效快照。</summary>
        /// <param name="values">待写入子表的快照值。</param>
        /// <param name="name">用于错误提示的字段名。</param>
        /// <param name="allowEmpty">全部范围的组关系可以为空，其他选项必须非空。</param>
        private static void ValidateValues(IReadOnlyList<string> values, string name, bool allowEmpty)
        {
            ArgumentNullException.ThrowIfNull(values, name);
            if ((!allowEmpty && values.Count == 0) || values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            {
                throw new ArgumentException("持久化选项不能为空、空白或重复。", name);
            }
        }

        /// <summary>校验持久化语言代码，防止写入未支持的语言。</summary>
        /// <param name="language">要读写的持久化语言代码。</param>
        private static void ValidateLanguage(string language)
        {
            if (language is not ("Japanese" or "English"))
            {
                throw new ArgumentOutOfRangeException(nameof(language));
            }
        }

        /// <summary>只用于查询主表列的内部 Dapper 行对象。</summary>
        private sealed class PlanRow
        {
            public string Language { get; set; } = string.Empty;
            public int DailyReviewTarget { get; set; }
            public int SessionWordLimit { get; set; }
            public string RangeMode { get; set; } = string.Empty;
            public string EnglishFormLabelMode { get; set; } = string.Empty;
            public string? EnglishExampleSourceSenseId { get; set; }
            public string UpdatedAtUtc { get; set; } = string.Empty;
        }

        /// <summary>只用于删除事务检查目标组的语言和位置。</summary>
        private sealed class GroupStateRow
        {
            public string Language { get; set; } = string.Empty;
            public string Placement { get; set; } = string.Empty;
        }
    }
}
