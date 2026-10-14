using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.DataAccess.Review;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>用真实三层证明每日复习只统计本地当天的不同整组且不修改成绩。</summary>
    public sealed class ReviewProgressServiceTests
    {
        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public async Task GetToday_EmptyLibraryShowsDefaultTargetWithoutCreatingState(LearningLanguage language)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            Assert.Equal(new ReviewDailyProgressDto(language, 0, 20), await Reader(environment).GetTodayAsync(language, Token));
            await using var connection = environment.Connections.CreateConnection();
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ReviewSessions;"));
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ReviewFormMemories;"));
        }

        [Fact]
        public async Task GetToday_UsesCurrentTargetNotActiveSessionSnapshot()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync(decision: ImportDecision.Review);
            await SaveAsync(environment, 7);
            var active = Assert.IsType<ReviewSessionDto>(await environment.Provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, Token));
            await SaveAsync(environment, 3);
            Assert.Equal(new ReviewDailyProgressDto(LearningLanguage.Japanese, 0, 3), await Reader(environment).GetTodayAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(7, active.PlanSnapshot.DailyReviewTarget);
        }

        [Fact]
        public async Task GetToday_CountsDistinctGroupsByCompletionDateAndLanguage()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var first = await environment.ImportAsync(decision: ImportDecision.Review);
            var second = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            await SeedAsync(environment, "first", first, "2026-09-30");
            await SeedAsync(environment, "repeat", first, "2026-09-30");
            await SeedAsync(environment, "yesterday", second, "2026-09-29");
            await SeedAsync(environment, "future", second, "2026-10-01");
            Assert.Equal(new ReviewDailyProgressDto(LearningLanguage.Japanese, 1, 20), await Reader(environment).GetTodayAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(new ReviewDailyProgressDto(LearningLanguage.English, 0, 20), await Reader(environment).GetTodayAsync(LearningLanguage.English, Token));
        }

        [Fact]
        public async Task GetToday_MultipleFormsAndAnswersCompleteExactlyOneGroup()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync(decision: ImportDecision.Review);
            await SaveAsync(environment, 20, new[] { "Dictionary", "Polite" });
            var session = Assert.IsType<ReviewSessionDto>(await environment.Provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, Token));
            var questions = environment.Provider.GetRequiredService<IReviewQuestionReader>();
            var answerer = environment.Provider.GetRequiredService<IReviewSessionAnswerer>();
            var count = 0;
            while (await questions.GetCurrentQuestionAsync(session.SessionId, Token) is { } question)
            {
                Assert.True(count++ < 10, "两个形式应该能在有限次数内完成。");
                await using var connection = environment.Connections.CreateConnection();
                var text = await connection.QuerySingleAsync<string>("""
                    SELECT Forms.Surface FROM WordGroupForms AS Forms
                    INNER JOIN ReviewQuestions AS Questions ON Questions.MemberId=Forms.MemberId AND Questions.FormCode=Forms.FormCode
                    WHERE Questions.QuestionId=@QuestionId AND Forms.ScriptCode='Kana'
                    ORDER BY Forms.IsPrimary DESC, Forms.IsCommon DESC, Forms.FormIndex LIMIT 1;
                    """, new { question.QuestionId });
                var result = await answerer.SubmitAnswerAsync(new(session.SessionId, question.QuestionId, text), Token);
                Assert.True(result.WasCorrect);
                if (count == 1)
                {
                    Assert.Equal(new ReviewDailyProgressDto(LearningLanguage.Japanese, 0, 20), await Reader(environment).GetTodayAsync(LearningLanguage.Japanese, Token));
                }
            }
            Assert.Equal(4, count);
            Assert.Equal(new ReviewDailyProgressDto(LearningLanguage.Japanese, 1, 20), await Reader(environment).GetTodayAsync(LearningLanguage.Japanese, Token));
        }

        [Fact]
        public async Task GetToday_DeletingWholeGroupRemovesItsCompletedProgress()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            await SeedAsync(environment, "completed", group, "2026-09-30");
            Assert.Equal(1, (await Reader(environment).GetTodayAsync(LearningLanguage.Japanese, Token)).CompletedGroupCount);
            await environment.Provider.GetRequiredService<IReviewLibraryManager>().DeleteWordGroupAsync(LearningLanguage.Japanese, group, Token);
            Assert.Equal(new ReviewDailyProgressDto(LearningLanguage.Japanese, 0, 20), await Reader(environment).GetTodayAsync(LearningLanguage.Japanese, Token));
        }

        [Fact]
        public async Task GetToday_UsesLocalDateRatherThanUtcDate()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            await SeedAsync(environment, "local-today", group, "2026-09-30");
            var service = new ReviewProgressService(environment.Provider.GetRequiredService<ReviewSessionDataAccessor>(),
                environment.Provider.GetRequiredService<IReviewPlanReader>(), new LocalBoundaryClock());
            Assert.Equal(new ReviewDailyProgressDto(LearningLanguage.Japanese, 1, 20), await service.GetTodayAsync(LearningLanguage.Japanese, Token));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(2)]
        public async Task GetToday_InvalidLanguageIsRejected(int language)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Reader(environment).GetTodayAsync((LearningLanguage)language, Token));
        }

        [Fact]
        public async Task GetToday_CancellationIsNotReportedAsZeroProgress()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader(environment).GetTodayAsync(LearningLanguage.Japanese, cancellation.Token));
        }

        [Fact]
        public async Task GetToday_DatabaseFailureIsNotReportedAsZeroProgress()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await using var connection = environment.Connections.CreateConnection();
            await connection.ExecuteAsync("DROP TABLE ReviewSessionGroups;");
            await Assert.ThrowsAsync<SqliteException>(() => Reader(environment).GetTodayAsync(LearningLanguage.Japanese, Token));
        }

        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static IReviewProgressReader Reader(MemorySeedTestEnvironment environment) => environment.Provider.GetRequiredService<IReviewProgressReader>();
        private static Task<ReviewPlanDto> SaveAsync(MemorySeedTestEnvironment environment, int target, string[]? forms = null) =>
            environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(new SaveReviewPlanRequestDto(LearningLanguage.Japanese,
                target, 20, ReviewRangeMode.All, Array.Empty<string>(), forms ?? new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting }, new[] { LearningOutputMode.Kana }, EnglishFormLabelMode.GrammarName, null), Token);

        private static async Task SeedAsync(MemorySeedTestEnvironment environment, string session, string group, string completedDate)
        {
            await using var connection = environment.Connections.CreateConnection();
            await connection.ExecuteAsync("""
                INSERT INTO ReviewSessions (SessionId, Language, Status, PlanJson, CreatedAtUtc, StartedLocalDate)
                VALUES (@Session, 'Japanese', 'Completed', '{}', '2026-09-29T00:00:00Z', '2026-09-29');
                INSERT INTO ReviewSessionGroups (SessionId, WordGroupId, SortOrder, CompletedLocalDate)
                VALUES (@Session, @Group, 0, @CompletedDate);
                """, new { Session = session, Group = group, CompletedDate = completedDate });
        }

        /// <summary>固定 UTC 前一天晚间与本地次日凌晨的日期边界。</summary>
        private sealed class LocalBoundaryClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(2026, 9, 29, 20, 30, 0, TimeSpan.Zero);
            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("测试UTC+8", TimeSpan.FromHours(8), "测试UTC+8", "测试UTC+8");
        }
    }
}
