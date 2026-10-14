using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Practice;
using Dictation.Core.Review;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证删除未完成复习词组后的会话收尾、独立成绩保留与事务回滚。</summary>
    public sealed class ReviewDeletionBoundaryTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task Delete_LastUnfinishedGroupClosesSessionWithoutRemovingCompletedProgress()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var state = await PrepareAsync(environment);
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            var before = await SnapshotAsync(connection, state.Completed);

            await environment.Provider.GetRequiredService<IReviewLibraryManager>().DeleteWordGroupAsync(
                LearningLanguage.Japanese, state.Unfinished, Token);

            Assert.Equal("Completed", await connection.ExecuteScalarAsync<string>(
                "SELECT Status FROM ReviewSessions WHERE SessionId=@Id;", new { Id = state.Session }));
            Assert.Equal(before, await SnapshotAsync(connection, state.Completed));
            Assert.Null(await environment.Provider.GetRequiredService<IReviewSessionReader>().GetActiveAsync(LearningLanguage.Japanese, Token));
            Assert.Null(await environment.Provider.GetRequiredService<IReviewQuestionReader>().GetCurrentQuestionAsync(state.Session, Token));
            Assert.Equal(1, (await environment.Provider.GetRequiredService<IReviewProgressReader>().GetTodayAsync(LearningLanguage.Japanese, Token)).CompletedGroupCount);
            await environment.ImportAsync("食事", "jmdict:10:1", ImportDecision.Review);
            var next = Assert.IsType<ReviewSessionDto>(await environment.Provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.NotEqual(state.Session, next.SessionId);
        }

        [Fact]
        public async Task Delete_AnotherUnfinishedGroupRemainsResumable()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var state = await PrepareAsync(environment, includeThird: true);
            await environment.Provider.GetRequiredService<IReviewLibraryManager>().DeleteWordGroupAsync(LearningLanguage.Japanese, state.Unfinished, Token);
            var active = Assert.IsType<ReviewSessionDto>(await environment.Provider.GetRequiredService<IReviewSessionReader>().GetActiveAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(state.Session, active.SessionId);
            Assert.NotNull(await environment.Provider.GetRequiredService<IReviewQuestionReader>().GetCurrentQuestionAsync(state.Session, Token));
        }

        [Fact]
        public async Task Delete_SessionClosureFailureRollsBackGroupAndHistoryDeletion()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var state = await PrepareAsync(environment);
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            var before = await SnapshotAsync(connection, state.Completed);
            await connection.ExecuteAsync("""
                CREATE TRIGGER RejectReviewClosure BEFORE UPDATE OF Status ON ReviewSessions
                WHEN NEW.Status='Completed' BEGIN SELECT RAISE(ABORT, '拒绝会话收尾'); END;
                """);
            await Assert.ThrowsAsync<SqliteException>(() => environment.Provider.GetRequiredService<IReviewLibraryManager>()
                .DeleteWordGroupAsync(LearningLanguage.Japanese, state.Unfinished, Token));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM WordGroups WHERE WordGroupId=@Id;", new { Id = state.Unfinished }));
            Assert.Equal("InProgress", await connection.ExecuteScalarAsync<string>("SELECT Status FROM ReviewSessions WHERE SessionId=@Id;", new { Id = state.Session }));
            Assert.Equal(before, await SnapshotAsync(connection, state.Completed));
        }

        /// <summary>通过真实答题完成第一个词组，其余词组只答错，形成有成绩的未完成会话。</summary>
        private static async Task<(string Session, string Completed, string Unfinished)> PrepareAsync(MemorySeedTestEnvironment environment, bool includeThird = false)
        {
            var first = await environment.ImportAsync(decision: ImportDecision.Review);
            var second = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            if (includeThird) await environment.ImportAsync("食事", "jmdict:10:1", ImportDecision.Review);
            await environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                ReviewRangeMode.All, Array.Empty<string>(), new[] { "Dictionary" }, new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana }, EnglishFormLabelMode.GrammarName, null), Token);
            var session = Assert.IsType<ReviewSessionDto>(await environment.Provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, Token));
            var reader = environment.Provider.GetRequiredService<IReviewQuestionReader>();
            var question = Assert.IsType<PracticeQuestionDto>(await reader.GetCurrentQuestionAsync(session.SessionId, Token));
            var completed = question.WordGroupId;
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            for (var index = 0; index < 30; index++)
            {
                var answer = question.WordGroupId == completed
                    ? await connection.ExecuteScalarAsync<string>("""
                        SELECT Forms.Surface FROM WordGroupForms Forms JOIN ReviewQuestions Questions ON Questions.MemberId=Forms.MemberId
                        WHERE Questions.QuestionId=@QuestionId AND Forms.FormCode=Questions.FormCode AND Forms.ScriptCode=Questions.OutputMode
                        ORDER BY Forms.IsPrimary DESC, Forms.FormIndex LIMIT 1;
                        """, new { question.QuestionId }) : "错误";
                var result = await environment.Provider.GetRequiredService<IReviewSessionAnswerer>().SubmitAnswerAsync(new(session.SessionId, question.QuestionId, answer), Token);
                if (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ReviewSessionGroups WHERE WordGroupId=@Id AND CompletedLocalDate IS NOT NULL;", new { Id = completed }) == 1)
                    return (session.SessionId, completed, completed == first ? second : first);
                question = Assert.IsType<PracticeQuestionDto>(result.NextQuestion);
            }
            throw new InvalidOperationException("夹具未能完成一个复习词组。");
        }

        /// <summary>比较剩余词组的持久化记忆、作答记录和完成日期，防止删除旁组误伤成绩。</summary>
        private static async Task<string> SnapshotAsync(SqliteConnection connection, string id)
        {
            var memory = await connection.QueryAsync("SELECT * FROM ReviewFormMemories WHERE WordGroupId=@Id ORDER BY MemberId,FormCode;", new { Id = id });
            var history = await connection.QueryAsync("SELECT * FROM ReviewAnswerAttempts WHERE WordGroupId=@Id ORDER BY SessionId,QuestionOrdinal,MemberId,FormCode;", new { Id = id });
            var date = await connection.ExecuteScalarAsync<string>("SELECT CompletedLocalDate FROM ReviewSessionGroups WHERE WordGroupId=@Id;", new { Id = id });
            return System.Text.Json.JsonSerializer.Serialize(new { Memory = memory, History = history, Date = date });
        }
    }
}
