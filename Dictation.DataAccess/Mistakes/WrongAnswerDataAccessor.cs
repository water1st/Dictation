using Dapper;
using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Mistakes
{
    /// <summary>只读查询原始独立错误观察和用户快照，不另建错题表或修改进度。</summary>
    public sealed class WrongAnswerDataAccessor
    {
        private readonly SqliteConnectionFactory connections;

        /// <summary>注入用户数据库连接工厂，每次查询独立创建连接。</summary>
        /// <param name="connections">唯一用户数据库的连接工厂。</param>
        public WrongAnswerDataAccessor(SqliteConnectionFactory connections)
        {
            this.connections = connections ?? throw new ArgumentNullException(nameof(connections));
        }

        /// <summary>联表读取指定语言的全部历史错误；主题和次要目标分别计数。</summary>
        /// <param name="language">数据库语言码。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        /// <returns>每个真实目标错误一条，不从复习题目表重复统计。</returns>
        public async Task<IReadOnlyList<WrongAnswerObservationPo>> LoadAsync(string language, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(language);
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = connections.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            var rows = await connection.QueryAsync<WrongAnswerObservationPo>(new CommandDefinition("""
                WITH Observations AS (
                    SELECT Q.WordGroupId,Q.MemberId,Q.FormCode,Q.Channel,Q.OutputMode,
                        A.SubmittedText,A.AnsweredAtUtc,'Learning' AS Mode
                    FROM LearningQuestions Q JOIN LearningQuestionAttempts A ON A.QuestionId=Q.QuestionId
                    WHERE A.WasCorrect=0
                    UNION ALL
                    SELECT WordGroupId,MemberId,FormCode,Channel,OutputMode,SubmittedText,AnsweredAtUtc,'Learning'
                    FROM LearningSharedAnswerAttempts WHERE WasCorrect=0
                    UNION ALL
                    SELECT WordGroupId,MemberId,FormCode,Channel,OutputMode,SubmittedText,AnsweredAtUtc,'Review'
                    FROM ReviewAnswerAttempts WHERE WasCorrect=0
                )
                SELECT O.WordGroupId,M.SourceSenseId,O.FormCode,M.Headword,G.Placement,
                    COALESCE((SELECT F.FormLabel FROM WordGroupForms F WHERE F.MemberId=M.MemberId
                        AND F.FormCode=O.FormCode ORDER BY F.IsPrimary DESC,F.FormIndex LIMIT 1),'') AS FormLabel,
                    CASE WHEN M.MeaningLanguage='zh-CN' THEN COALESCE((SELECT group_concat(Text,'；') FROM
                        (SELECT Text FROM WordGroupMeanings WHERE MemberId=M.MemberId ORDER BY MeaningIndex)),'')
                        ELSE '' END AS Meanings,
                    R.DueAtUtc,O.Mode,O.Channel,O.OutputMode,O.SubmittedText,O.AnsweredAtUtc,
                    COALESCE((SELECT F.Surface FROM WordGroupForms F WHERE F.MemberId=M.MemberId
                        AND F.FormCode=O.FormCode AND (M.Language='English' OR F.ScriptCode=O.OutputMode)
                        ORDER BY F.IsPrimary DESC,F.FormIndex LIMIT 1),'') AS ExpectedAnswer,
                    COALESCE((SELECT CASE WHEN M.Language='English' THEN F.Surface ELSE COALESCE(F.Reading,F.Surface) END
                        FROM WordGroupForms F WHERE F.MemberId=M.MemberId
                        AND F.FormCode=O.FormCode AND (M.Language='English' OR F.ScriptCode=O.OutputMode)
                        ORDER BY F.IsPrimary DESC,F.FormIndex LIMIT 1),'') AS ExpectedSpokenText
                FROM Observations O JOIN WordGroups G ON G.WordGroupId=O.WordGroupId
                JOIN WordGroupMembers M ON M.MemberId=O.MemberId AND M.WordGroupId=O.WordGroupId
                LEFT JOIN ReviewFormMemories R ON R.MemberId=O.MemberId AND R.FormCode=O.FormCode
                WHERE G.Language=@Language AND M.Language=@Language
                ORDER BY O.AnsweredAtUtc DESC,O.WordGroupId,M.SourceSenseId,O.FormCode;
                """, new { Language = language }, cancellationToken: cancellationToken));
            return rows.ToArray();
        }
    }
}
