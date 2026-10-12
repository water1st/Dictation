using System.Globalization;
using Dapper;
using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Statistics
{
    /// <summary>保存每日目标并读取原完成事实；不执行打卡业务判定。</summary>
    public sealed class DailyCheckInDataAccessor
    {
        private readonly SqliteConnectionFactory connections;

        /// <summary>注入独立用户数据库连接工厂。</summary>
        /// <param name="connections">每次查询创建独立连接。</param>
        public DailyCheckInDataAccessor(SqliteConnectionFactory connections) => this.connections = connections ?? throw new ArgumentNullException(nameof(connections));

        /// <summary>只读查询指定日期快照；尚未练习返回空。</summary>
        /// <param name="language">持久化语言码。</param>
        /// <param name="localDate">严格 yyyy-MM-dd 日期。</param>
        /// <param name="cancellationToken">取消查询。</param>
        public async Task<DailyPlanSnapshotPo?> GetSnapshotAsync(string language, string localDate, CancellationToken cancellationToken = default)
        {
            Validate(language, localDate);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<DailyPlanSnapshotPo>(new CommandDefinition(
                "SELECT * FROM DailyPlanSnapshots WHERE Language=@language AND LocalDate=@localDate;", new { language, localDate }, cancellationToken: cancellationToken));
        }

        /// <summary>只保存首次目标；并发或重复启动不会覆盖已记录目标。</summary>
        /// <param name="snapshot">业务层计算的有效目标。</param>
        /// <param name="cancellationToken">取消写入。</param>
        public async Task InsertIfAbsentAsync(DailyPlanSnapshotPo snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            Validate(snapshot.Language, snapshot.LocalDate);
            ArgumentOutOfRangeException.ThrowIfNegative(snapshot.LearningTarget);
            ArgumentOutOfRangeException.ThrowIfNegative(snapshot.ReviewTarget);
            ArgumentOutOfRangeException.ThrowIfNegative(snapshot.KanaTarget);
            if (snapshot.Language == "English" && snapshot.KanaTarget != 0) throw new ArgumentException("英语不能包含五十音目标。", nameof(snapshot));
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO DailyPlanSnapshots(Language,LocalDate,LearningTarget,ReviewTarget,KanaTarget)
                VALUES(@Language,@LocalDate,@LearningTarget,@ReviewTarget,@KanaTarget)
                ON CONFLICT(Language,LocalDate) DO NOTHING;
                """, snapshot, cancellationToken: cancellationToken));
        }

        /// <summary>单次查询读取三种完成事实；整组删除的级联结果立即可见。</summary>
        /// <param name="language">持久化语言码。</param>
        /// <param name="localDate">实际完成日期。</param>
        /// <param name="cancellationToken">取消读取。</param>
        public async Task<DailyCompletionCountsPo> GetCompletionCountsAsync(string language, string localDate, CancellationToken cancellationToken = default)
        {
            Validate(language, localDate);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.QuerySingleAsync<DailyCompletionCountsPo>(new CommandDefinition("""
                WITH LearningFacts AS (
                    SELECT WordGroupId,LocalDate FROM LearningCompletionDates
                    UNION SELECT WordGroupId,CompletedLocalDate FROM LearningGroupProgress WHERE CompletedLocalDate IS NOT NULL
                )
                SELECT
                  (SELECT COUNT(DISTINCT F.WordGroupId) FROM LearningFacts F JOIN WordGroups G ON G.WordGroupId=F.WordGroupId
                    WHERE G.Language=@language AND F.LocalDate=@localDate) AS LearningCompleted,
                  (SELECT COUNT(DISTINCT R.WordGroupId) FROM ReviewSessionGroups R
                    JOIN ReviewSessions S ON S.SessionId=R.SessionId JOIN WordGroups G ON G.WordGroupId=R.WordGroupId
                    WHERE G.Language=@language AND S.Language=@language AND R.CompletedLocalDate=@localDate) AS ReviewCompleted,
                  CASE WHEN @language='Japanese' THEN
                    (SELECT COUNT(DISTINCT KanaId) FROM KanaSessionItems WHERE IsCompleted=1 AND CompletedLocalDate=@localDate)
                    ELSE 0 END AS KanaCompleted;
                """, new { language, localDate }, cancellationToken: cancellationToken));
        }

        /// <summary>读取某模式完成的不同组 ID，供业务层对可练与已完成范围求并集。</summary>
        /// <param name="language">持久化语言码。</param>
        /// <param name="localDate">实际完成日期。</param>
        /// <param name="review">true 查询复习，false 查询学习。</param>
        /// <param name="cancellationToken">取消查询。</param>
        public async Task<IReadOnlyList<string>> GetCompletedGroupIdsAsync(string language, string localDate, bool review, CancellationToken cancellationToken = default)
        {
            Validate(language, localDate);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            var sql = review ? """
                SELECT DISTINCT R.WordGroupId FROM ReviewSessionGroups R JOIN ReviewSessions S ON S.SessionId=R.SessionId
                JOIN WordGroups G ON G.WordGroupId=R.WordGroupId
                WHERE G.Language=@language AND S.Language=@language AND R.CompletedLocalDate=@localDate;
                """ : """
                SELECT F.WordGroupId FROM LearningCompletionDates F JOIN WordGroups G ON G.WordGroupId=F.WordGroupId
                WHERE G.Language=@language AND F.LocalDate=@localDate
                UNION SELECT P.WordGroupId FROM LearningGroupProgress P JOIN WordGroups G ON G.WordGroupId=P.WordGroupId
                WHERE G.Language=@language AND P.CompletedLocalDate=@localDate;
                """;
            return (await connection.QueryAsync<string>(new CommandDefinition(sql, new { language, localDate }, cancellationToken: cancellationToken))).ToArray();
        }

        /// <summary>在单次快照内读取月区间的目标与事实，资格判定留在业务层。</summary>
        /// <param name="year">1–9999年。</param>
        /// <param name="month">1–12月。</param>
        /// <param name="cancellationToken">取消读取。</param>
        public async Task<IReadOnlyList<CalendarDayCountsPo>> LoadMonthAsync(int year, int month, CancellationToken cancellationToken = default)
        {
            var first = new DateOnly(year, month, 1);
            cancellationToken.ThrowIfCancellationRequested();
            var start = first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            // 使用排序上界而非构造10000年；所有保存的完成日都已经严格验证。
            var end = year == 9999 && month == 12 ? "9999-12-32" : first.AddMonths(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction(deferred: true);
            var rows = (await connection.QueryAsync<CalendarDayCountsPo>(new CommandDefinition("""
                WITH LearningFacts AS (
                    SELECT WordGroupId,LocalDate FROM LearningCompletionDates
                    UNION SELECT WordGroupId,CompletedLocalDate FROM LearningGroupProgress WHERE CompletedLocalDate IS NOT NULL
                ), Learning AS (
                    SELECT G.Language,F.LocalDate,COUNT(DISTINCT F.WordGroupId) AS Value
                    FROM LearningFacts F JOIN WordGroups G ON G.WordGroupId=F.WordGroupId
                    WHERE F.LocalDate>=@start AND F.LocalDate<@end GROUP BY G.Language,F.LocalDate
                ), Review AS (
                    SELECT S.Language,R.CompletedLocalDate AS LocalDate,COUNT(DISTINCT R.WordGroupId) AS Value
                    FROM ReviewSessionGroups R JOIN ReviewSessions S ON S.SessionId=R.SessionId
                    JOIN WordGroups G ON G.WordGroupId=R.WordGroupId AND G.Language=S.Language
                    WHERE R.CompletedLocalDate>=@start AND R.CompletedLocalDate<@end GROUP BY S.Language,R.CompletedLocalDate
                ), Kana AS (
                    SELECT 'Japanese' AS Language,CompletedLocalDate AS LocalDate,COUNT(DISTINCT KanaId) AS Value
                    FROM KanaSessionItems WHERE IsCompleted=1 AND CompletedLocalDate>=@start AND CompletedLocalDate<@end
                    GROUP BY CompletedLocalDate
                ), Dates AS (
                    SELECT Language,LocalDate FROM DailyPlanSnapshots WHERE LocalDate>=@start AND LocalDate<@end
                    UNION SELECT Language,LocalDate FROM Learning
                    UNION SELECT Language,LocalDate FROM Review
                    UNION SELECT Language,LocalDate FROM Kana
                )
                SELECT D.Language,D.LocalDate,COALESCE(L.Value,0) AS LearningCompleted,
                    COALESCE(R.Value,0) AS ReviewCompleted,COALESCE(K.Value,0) AS KanaCompleted,
                    P.LearningTarget,P.ReviewTarget,P.KanaTarget
                FROM Dates D
                LEFT JOIN DailyPlanSnapshots P ON P.Language=D.Language AND P.LocalDate=D.LocalDate
                LEFT JOIN Learning L ON L.Language=D.Language AND L.LocalDate=D.LocalDate
                LEFT JOIN Review R ON R.Language=D.Language AND R.LocalDate=D.LocalDate
                LEFT JOIN Kana K ON K.Language=D.Language AND K.LocalDate=D.LocalDate
                ORDER BY D.LocalDate,D.Language;
                """, new { start, end }, transaction, cancellationToken: cancellationToken))).ToArray();
            await transaction.CommitAsync(cancellationToken);
            return rows;
        }

        /// <summary>拒绝未知语言和不可用的本地日期，防止非标准日期破坏区间查询。</summary>
        /// <param name="language">两个受支持语言之一。</param>
        /// <param name="localDate">严格日期文本。</param>
        private static void Validate(string language, string localDate)
        {
            if (language is not ("Japanese" or "English")) throw new ArgumentOutOfRangeException(nameof(language), "统计语言无效。");
            if (!DateOnly.TryParseExact(localDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw new ArgumentException("统计日期必须为 yyyy-MM-dd。", nameof(localDate));
        }
    }
}
