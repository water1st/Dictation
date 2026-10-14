using System.Globalization;
using Dapper;
using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Statistics
{
    /// <summary>只读查询词组完成事实，不以导入量或词形数代替已学数量。</summary>
    public sealed class LearningStatisticsDataAccessor
    {
        private readonly SqliteConnectionFactory connections;

        /// <summary>注入每次查询独立创建连接的工厂。</summary>
        /// <param name="connections">用户数据库连接工厂。</param>
        public LearningStatisticsDataAccessor(SqliteConnectionFactory connections) => this.connections = connections ?? throw new ArgumentNullException(nameof(connections));

        /// <summary>按现存整组去重统计全部完成日和当天完成日。</summary>
        /// <param name="language">Japanese 或 English。</param>
        /// <param name="localDate">严格 yyyy-MM-dd 本地日期。</param>
        /// <param name="cancellationToken">取消读取。</param>
        /// <returns>不包括五十音、未完成导入词或已经删除的组。</returns>
        public async Task<LearningStatisticsCountsPo> LoadAsync(string language, string localDate, CancellationToken cancellationToken = default)
        {
            if (language is not ("Japanese" or "English")) throw new ArgumentOutOfRangeException(nameof(language));
            if (!DateOnly.TryParseExact(localDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw new ArgumentException("统计日期必须为 yyyy-MM-dd。", nameof(localDate));
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return await connection.QuerySingleAsync<LearningStatisticsCountsPo>(new CommandDefinition("""
                WITH Facts AS (
                    SELECT WordGroupId,LocalDate FROM LearningCompletionDates
                    UNION SELECT WordGroupId,CompletedLocalDate FROM LearningGroupProgress WHERE CompletedLocalDate IS NOT NULL
                )
                SELECT COUNT(DISTINCT F.WordGroupId) AS LearnedGroupCount,
                    COUNT(DISTINCT CASE WHEN F.LocalDate=@localDate THEN F.WordGroupId END) AS TodayLearnedGroupCount
                FROM Facts F JOIN WordGroups G ON G.WordGroupId=F.WordGroupId WHERE G.Language=@language;
                """, new { language, localDate }, cancellationToken: cancellationToken));
        }
    }
}
