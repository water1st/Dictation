using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Runtime;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Review
{
    /// <summary>负责复习会话、范围和选定成员形式的事务访问，不执行风险算法。</summary>
    public sealed class ReviewSessionDataAccessor
    {
        private readonly SqliteConnectionFactory connectionFactory;
        private readonly WordGroupMemberSnapshotReader memberReader;
        /// <summary>组合同一用户数据库的连接与完整成员快照读取。</summary>
        /// <param name="connectionFactory">用户连接工厂。</param>
        /// <param name="memberReader">复用调用方事务读取词组成员的组件。</param>
        internal ReviewSessionDataAccessor(SqliteConnectionFactory connectionFactory, WordGroupMemberSnapshotReader memberReader)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.memberReader = memberReader ?? throw new ArgumentNullException(nameof(memberReader));
        }

        /// <summary>读取仍有词组的指定语言活动会话。</summary>
        /// <param name="language">Japanese 或 English。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        public async Task<ReviewSessionPo?> GetActiveAsync(string language, CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction(deferred: true);
            var result = await ReadActiveAsync(connection, transaction, language, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }

        /// <summary>只读指定轮次尚未完成的组，不改变完整轮次的浏览与结算合同。</summary>
        /// <param name="sessionId">已读取活动轮次的稳定标识。</param>
        /// <param name="cancellationToken">取消查询。</param>
        /// <returns>未记录完成日的整组 ID；已完成或删除的组不返回。</returns>
        public async Task<IReadOnlyList<string>> GetIncompleteWordGroupIdsAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return (await connection.QueryAsync<string>(new CommandDefinition("""
                SELECT G.WordGroupId FROM ReviewSessionGroups G JOIN ReviewSessions S ON S.SessionId=G.SessionId
                WHERE G.SessionId=@sessionId AND S.Status='InProgress' AND G.CompletedLocalDate IS NULL
                ORDER BY G.SortOrder;
                """, new { sessionId }, cancellationToken: cancellationToken))).ToArray();
        }

        /// <summary>在写事务内验证归属并创建会话；并发启动返回已有活动会话。</summary>
        /// <param name="request">已由业务层准备的会话与形式。</param>
        /// <param name="cancellationToken">取消事务的令牌。</param>
        public async Task<ReviewSessionPo> CreateAsync(CreateReviewSessionPo request, CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            // 写事务先串行化同语言启动；其它调用返回胜者，不替换其计划或选词。
            await using var transaction = connection.BeginTransaction();
            var active = await ReadActiveAsync(connection, transaction, request.Language, cancellationToken);
            if (active is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return active;
            }

            // 用户整组全删后，外键已清空范围和形式；仅在下一次写入清理空活动记录以释放唯一约束。
            await connection.ExecuteAsync(new CommandDefinition("""
                DELETE FROM ReviewSessions WHERE Language=@Language AND Status='InProgress'
                    AND NOT EXISTS (SELECT 1 FROM ReviewSessionGroups WHERE ReviewSessionGroups.SessionId=ReviewSessions.SessionId);
                """, new { request.Language }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO ReviewSessions (SessionId, Language, Status, PlanJson, CreatedAtUtc, StartedLocalDate)
                VALUES (@SessionId, @Language, 'InProgress', @PlanJson, @CreatedAtUtc, @LocalDate);
                """, new { request.SessionId, request.Language, request.PlanJson,
                    CreatedAtUtc = request.CreatedAtUtc.ToUniversalTime().ToString("O"), request.LocalDate }, transaction, cancellationToken: cancellationToken));
            for (var index = 0; index < request.Groups.Count; index++)
            {
                var group = request.Groups[index];
                var validGroup = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                    SELECT EXISTS (SELECT 1 FROM WordGroups WHERE WordGroupId=@WordGroupId AND Language=@Language AND Placement='Review');
                    """, new { group.WordGroupId, request.Language }, transaction, cancellationToken: cancellationToken));
                if (!validGroup)
                {
                    throw new ArgumentException("所选复习词组已删除或不属于当前语言复习库。", nameof(request));
                }
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO ReviewSessionGroups (SessionId, WordGroupId, SortOrder) VALUES (@SessionId, @WordGroupId, @SortOrder);
                    """, new { request.SessionId, group.WordGroupId, SortOrder = index }, transaction, cancellationToken: cancellationToken));
                foreach (var form in group.Forms)
                {
                    var member = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("""
                        SELECT DISTINCT Members.MemberId FROM WordGroupMembers AS Members
                        INNER JOIN WordGroupForms AS Forms ON Forms.MemberId=Members.MemberId
                        WHERE Members.WordGroupId=@WordGroupId AND Members.SourceSenseId=@SourceSenseId
                            AND Members.Language=@Language AND Forms.FormCode=@FormCode;
                        """, new { group.WordGroupId, form.SourceSenseId, form.FormCode, request.Language }, transaction, cancellationToken: cancellationToken));
                    if (member is null)
                    {
                        throw new ArgumentException("复习成员或形式不属于所选词组。", nameof(request));
                    }
                    await connection.ExecuteAsync(new CommandDefinition("""
                        INSERT INTO ReviewSessionForms (SessionId, WordGroupId, MemberId, FormCode)
                        VALUES (@SessionId, @WordGroupId, @MemberId, @FormCode);
                        """, new { request.SessionId, group.WordGroupId, MemberId = member.Value, form.FormCode }, transaction, cancellationToken: cancellationToken));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken);
            return new ReviewSessionPo(request.SessionId, request.Language, request.PlanJson,
                request.CreatedAtUtc.ToUniversalTime(), request.Groups.Select(group => group.WordGroupId).ToArray());
        }

        /// <summary>读取本轮完整中文快照及实际可用形式。</summary>
        /// <param name="sessionId">复习会话标识。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        public async Task<IReadOnlyList<ImportWordGroupPo>> GetBrowseItemsAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction(deferred: true);
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM ReviewSessions WHERE SessionId=@SessionId);",
                new { SessionId = sessionId }, transaction, cancellationToken: cancellationToken));
            if (!exists)
            {
                throw new KeyNotFoundException("复习会话不存在。");
            }
            var rows = (await connection.QueryAsync<ReviewLibraryGroupRow>(new CommandDefinition("""
                SELECT Groups.WordGroupId, Groups.Language, Groups.AnchorSourceSenseId, Groups.Placement, Groups.CreatedAtUtc
                FROM ReviewSessionGroups AS Selected INNER JOIN WordGroups AS Groups ON Groups.WordGroupId=Selected.WordGroupId
                WHERE Selected.SessionId=@SessionId ORDER BY Selected.SortOrder;
                """, new { SessionId = sessionId }, transaction, cancellationToken: cancellationToken))).ToArray();
            var members = await memberReader.ReadAsync(connection, transaction, rows.Select(row => row.WordGroupId).ToArray(), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ReviewLibrarySnapshotMapper.ToGroups(rows, members);
        }

        /// <summary>统计指定语言当天完成的不同整组，不按形式计数。</summary>
        /// <param name="language">持久化语言代码。</param>
        /// <param name="localDate">yyyy-MM-dd 的本地日期。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        public async Task<int> CountCompletedOnDateAsync(string language, string localDate, CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            ValidateDate(localDate);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(DISTINCT Groups.WordGroupId) FROM ReviewSessionGroups AS Groups
                INNER JOIN ReviewSessions AS Sessions ON Sessions.SessionId=Groups.SessionId
                WHERE Sessions.Language=@Language AND Groups.CompletedLocalDate=@LocalDate;
                """, new { Language = language, LocalDate = localDate }, cancellationToken: cancellationToken));
        }

        /// <summary>复用调用方事务，避免会话概要与范围来自不同时间点。</summary>
        /// <param name="connection">已打开的用户连接。</param>
        /// <param name="transaction">调用方持有的读／写事务。</param>
        /// <param name="language">查询的语言。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        private static async Task<ReviewSessionPo?> ReadActiveAsync(SqliteConnection connection, DbTransaction transaction,
            string language, CancellationToken cancellationToken)
        {
            var row = await connection.QuerySingleOrDefaultAsync<ReviewSessionHeaderRow>(new CommandDefinition("""
                SELECT SessionId, Language, PlanJson, CreatedAtUtc FROM ReviewSessions
                WHERE Language=@Language AND Status='InProgress'
                    AND EXISTS (SELECT 1 FROM ReviewSessionGroups WHERE ReviewSessionGroups.SessionId=ReviewSessions.SessionId);
                """, new { Language = language }, transaction, cancellationToken: cancellationToken));
            if (row is null)
            {
                return null;
            }
            var ids = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT WordGroupId FROM ReviewSessionGroups WHERE SessionId=@SessionId ORDER BY SortOrder;",
                new { row.SessionId }, transaction, cancellationToken: cancellationToken))).ToArray();
            return ReviewSessionRowMapper.ToPo(row, ids);
        }

        /// <summary>验证写入字段及集合结构，实际数据库归属仍在写事务内复查。</summary>
        /// <param name="request">会话创建输入。</param>
        private static void ValidateRequest(CreateReviewSessionPo request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateLanguage(request.Language);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
            ValidateDate(request.LocalDate);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.PlanJson);
            try
            {
                using var json = JsonDocument.Parse(request.PlanJson);
                if (json.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("计划快照必须是 JSON 对象。", nameof(request));
                }
            }
            catch (JsonException exception)
            {
                throw new ArgumentException("计划快照 JSON 无效。", nameof(request), exception);
            }
            ArgumentNullException.ThrowIfNull(request.Groups);
            if (request.Groups.Count == 0 || request.Groups.Select(group => group?.WordGroupId).Distinct(StringComparer.Ordinal).Count() != request.Groups.Count)
            {
                throw new ArgumentException("会话必须有至少一个不重复的整组。", nameof(request));
            }
            foreach (var group in request.Groups)
            {
                ArgumentNullException.ThrowIfNull(group);
                ArgumentException.ThrowIfNullOrWhiteSpace(group.WordGroupId);
                ArgumentNullException.ThrowIfNull(group.Forms);
                if (group.Forms.Count == 0)
                {
                    throw new ArgumentException("会话整组没有待练形式。", nameof(request));
                }
                var identities = new HashSet<(string, string)>();
                foreach (var form in group.Forms)
                {
                    ArgumentNullException.ThrowIfNull(form);
                    ArgumentException.ThrowIfNullOrWhiteSpace(form.SourceSenseId);
                    ArgumentException.ThrowIfNullOrWhiteSpace(form.FormCode);
                    if (!identities.Add((form.SourceSenseId, form.FormCode)))
                    {
                        throw new ArgumentException("会话成员形式不能重复。", nameof(request));
                    }
                }
            }
        }

        /// <summary>拒绝没有表内语言语义的代码。</summary>
        /// <param name="language">调用方语言代码。</param>
        private static void ValidateLanguage(string language)
        {
            if (language is not ("Japanese" or "English"))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "复习会话语言不受支持。");
            }
        }

        /// <summary>只接受规范本地日期，避免统计漏算或无法恢复的输入。</summary>
        /// <param name="localDate">yyyy-MM-dd 日期文本。</param>
        private static void ValidateDate(string localDate)
        {
            if (!DateOnly.TryParseExact(localDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                throw new ArgumentException("本地日期必须为 yyyy-MM-dd。", nameof(localDate));
            }
        }
    }
}
