using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Mistakes;
using Dictation.Core.Practice;
using Dictation.Core.Review;
using Dictation.DataAccess.Review;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Dictation.Tests.Core
{
    /// <summary>用真实作答、记忆和删除验证错题投影及既有复习链路的启动边界。</summary>
    public sealed class WrongAnswerServiceTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static IWrongAnswerQuery Query(MemorySeedTestEnvironment env) => env.Provider.GetRequiredService<IWrongAnswerQuery>();
        private static IWrongAnswerPractice Practice(MemorySeedTestEnvironment env) => env.Provider.GetRequiredService<IWrongAnswerPractice>();

        [Fact]
        public async Task Load_EmptyAndOtherLanguageDoesNotCreateHistory()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            Assert.Empty(await Query(env).LoadAsync(LearningLanguage.Japanese, Token));
            await LearningWrongAsync(env, shared: false, finish: false);
            Assert.Empty(await Query(env).LoadAsync(LearningLanguage.English, Token));
            Assert.Single(await Query(env).LoadAsync(LearningLanguage.Japanese, Token));
        }

        [Fact]
        public async Task Load_EnglishErrorHasChineseMeaningAndDoesNotLeakIntoJapanese()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var batch = await env.Provider.GetRequiredService<IImportStaging>().StageAsync(new(LearningLanguage.English,
                ImportSourceKind.Manual, new[] { new ImportSelectionDto("go", "go%2:01:00::") }), Token);
            await env.Provider.GetRequiredService<IImportInboxActions>().DecideAsync(new(batch.Batch.BatchId,
                new[] { Assert.Single(batch.Batch.Items).ItemId }, ImportDecision.Learning), Token);
            await env.Provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(new(LearningLanguage.English, 20, 20,
                LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null, new[] { "Base" },
                new[] { LearningChannel.Dictation }, new[] { LearningOutputMode.Latin }), Token);
            var session = await env.Sessions.StartOrResumeAsync(new(LearningLanguage.English), Token);
            var q = Assert.IsType<PracticeQuestionDto>(await env.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            while (q.SpokenText != "go")
                q = Assert.IsType<PracticeQuestionDto>((await env.Sessions.SubmitAnswerAsync(new(session.SessionId, q.QuestionId, q.SpokenText), Token)).NextQuestion);
            await env.Sessions.SubmitAnswerAsync(new(session.SessionId, q.QuestionId, "错误"), Token);
            var item = Assert.Single(await Query(env).LoadAsync(LearningLanguage.English, Token));
            Assert.Equal("go", item.Headword);
            Assert.Matches("[\u4e00-\u9fff]", item.Meanings);
            Assert.Equal("go", Assert.Single(item.Attempts).ExpectedAnswer);
            Assert.Empty(await Query(env).LoadAsync(LearningLanguage.Japanese, Token));
        }

        [Fact]
        public async Task Load_BrokenDatabaseDoesNotPretendToReturnEmptySuccess()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            // 仅删除本测试临时数据库中的错误观察表，验证读取故障不会伪装成“没有错题”。
            await using var connection = env.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            await connection.ExecuteAsync("DROP TABLE LearningSharedAnswerAttempts;");
            await Assert.ThrowsAsync<SqliteException>(() => Query(env).LoadAsync(LearningLanguage.Japanese, Token));
        }

        [Fact]
        public async Task Load_SharedLearningErrorsRetainsIndependentChineseDetailsAndCounts()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var session = await LearningWrongAsync(env, shared: true, finish: false);
            var question = Assert.IsType<PracticeQuestionDto>(await env.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            await env.Sessions.SubmitAnswerAsync(new(session.SessionId, question.QuestionId, "再次错误"), Token);
            var errors = await Query(env).LoadAsync(LearningLanguage.Japanese, Token);
            Assert.Equal(2, errors.Count);
            Assert.All(errors, item =>
            {
                Assert.Equal("食べる", item.Headword);
                Assert.Equal("Dictionary", item.FormCode);
                Assert.Equal("Learning", item.Placement);
                Assert.Equal(2, item.Attempts.Count);
                Assert.Contains(item.Attempts, attempt => attempt.SubmittedText == "再次错误");
                Assert.All(item.Attempts, attempt => Assert.Equal(LearningChannel.Dictation, attempt.Channel));
                Assert.Matches("[\u4e00-\u9fff]", item.Meanings);
                Assert.Null(item.DueAtUtc);
            });
            Assert.Equal(2, errors.Select(item => item.SourceSenseId).Distinct().Count());
        }

        [Fact]
        public async Task Load_DeleteSharedPrimaryKeepsOtherSenseError()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var session = await LearningWrongAsync(env, shared: true, finish: false);
            await using var connection = env.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            var primaryGroup = await connection.ExecuteScalarAsync<string>(
                "SELECT WordGroupId FROM LearningQuestions WHERE SessionId=@SessionId AND QuestionOrdinal=0;", new { session.SessionId });
            await env.Provider.GetRequiredService<ILearningLibraryManager>().DeleteWordGroupAsync(LearningLanguage.Japanese, primaryGroup!, Token);
            var error = Assert.Single(await Query(env).LoadAsync(LearningLanguage.Japanese, Token));
            Assert.NotEqual(primaryGroup, error.WordGroupId);
            Assert.Equal("错误", Assert.Single(error.Attempts).SubmittedText);
        }

        [Fact]
        public async Task Load_ReviewErrorIsCountedOnceAndCorrectRetryDoesNotEraseAudit()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            await env.ImportAsync(decision: ImportDecision.Review);
            await ReviewPlanAsync(env, "Dictionary");
            var session = Assert.IsType<ReviewSessionDto>(await env.Provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, Token));
            var reader = env.Provider.GetRequiredService<IReviewQuestionReader>();
            var answerer = env.Provider.GetRequiredService<IReviewSessionAnswerer>();
            var q = Assert.IsType<PracticeQuestionDto>(await reader.GetCurrentQuestionAsync(session.SessionId, Token));
            var feedback = await answerer.SubmitAnswerAsync(new(session.SessionId, q.QuestionId, "错误"), Token);
            while (feedback.NextQuestion is { } next)
                feedback = await answerer.SubmitAnswerAsync(new(session.SessionId, next.QuestionId, "たべる"), Token);
            var item = Assert.Single(await Query(env).LoadAsync(LearningLanguage.Japanese, Token));
            Assert.Equal("Review", Assert.Single(item.Attempts).Mode);
            Assert.Equal("Review", item.Placement);
            Assert.True(item.DueAtUtc > MemorySeedTestEnvironment.Now);
            Assert.Single(item.Attempts);
        }

        [Fact]
        public async Task Start_FutureDueWrongFormOnlyDoesNotChangePlanOrMemory()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var group = await env.ImportAsync();
            await env.SavePlanAsync("Dictionary", "Polite");
            await env.FinishAsync("Polite");
            await ReviewPlanAsync(env, "Dictionary", "Polite");
            var beforePlan = JsonSerializer.Serialize(await env.Provider.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.Japanese, Token));
            var before = await env.MemoriesAsync(group);
            var session = await Practice(env).StartOrResumeAsync(LearningLanguage.Japanese, new[] { group }, Token);
            Assert.Equal(group, Assert.Single(session.WordGroupIds));
            var snapshot = await env.Provider.GetRequiredService<ReviewQuestionDataAccessor>().GetSnapshotAsync(session.SessionId, Token);
            Assert.Equal("Polite", Assert.Single(snapshot.Units).FormCode);
            var item = Assert.Single(await Query(env).LoadAsync(LearningLanguage.Japanese, Token));
            Assert.True(item.DueAtUtc > MemorySeedTestEnvironment.Now);
            var after = await env.MemoriesAsync(group);
            Assert.Equal(before.Select(value => (string)value.DueAtUtc), after.Select(value => (string)value.DueAtUtc));
            Assert.Equal(before.Select(value => (long)value.ReviewCount), after.Select(value => (long)value.ReviewCount));
            Assert.Equal(beforePlan, JsonSerializer.Serialize(await env.Provider.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.Japanese, Token)));
        }

        [Fact]
        public async Task Start_ExistingReviewIsResumedWithoutReplacingItsGroups()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var learning = await LearningWrongAsync(env, shared: false, finish: true);
            var error = Assert.Single(await Query(env).LoadAsync(LearningLanguage.Japanese, Token));
            var other = await env.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            await ReviewPlanAsync(env, "Dictionary");
            var active = Assert.IsType<ReviewSessionDto>(await env.Provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, Token));
            Assert.Equal(other, Assert.Single(active.WordGroupIds));
            var resumed = await Practice(env).StartOrResumeAsync(LearningLanguage.Japanese, new[] { error.WordGroupId }, Token);
            Assert.Equal(active.SessionId, resumed.SessionId);
            Assert.Equal(other, Assert.Single(resumed.WordGroupIds));
        }

        [Fact]
        public async Task Start_DisabledWrongFormDoesNotCreateSession()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            var group = await env.ImportAsync();
            await env.SavePlanAsync("Dictionary", "Polite");
            await env.FinishAsync("Polite");
            await ReviewPlanAsync(env, "Dictionary");
            await Assert.ThrowsAsync<InvalidOperationException>(() => Practice(env).StartOrResumeAsync(LearningLanguage.Japanese, new[] { group }, Token));
            Assert.Null(await env.Provider.GetRequiredService<IReviewSessionReader>().GetActiveAsync(LearningLanguage.Japanese, Token));
        }

        [Fact]
        public async Task Start_RespectsSessionLimitAndSelectedPlanScope()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            await LearningWrongAsync(env, shared: true, finish: true);
            var groups = (await Query(env).LoadAsync(LearningLanguage.Japanese, Token)).Select(item => item.WordGroupId).ToArray();
            Assert.Equal(2, groups.Length);
            var editor = env.Provider.GetRequiredService<IReviewPlanEditor>();
            var request = new SaveReviewPlanRequestDto(LearningLanguage.Japanese, 20, 1, ReviewRangeMode.All,
                Array.Empty<string>(), new[] { "Dictionary" }, new[] { LearningChannel.Dictation },
                new[] { LearningOutputMode.Kana }, EnglishFormLabelMode.GrammarName, null);
            await editor.SaveAsync(request, Token);
            await Assert.ThrowsAsync<ArgumentException>(() => Practice(env).StartOrResumeAsync(LearningLanguage.Japanese, groups, Token));
            await editor.SaveAsync(request with { SessionWordLimit = 20, RangeMode = ReviewRangeMode.Selected, WordGroupIds = new[] { groups[0] } }, Token);
            await Assert.ThrowsAsync<ArgumentException>(() => Practice(env).StartOrResumeAsync(LearningLanguage.Japanese, new[] { groups[1] }, Token));
            Assert.Null(await env.Provider.GetRequiredService<IReviewSessionReader>().GetActiveAsync(LearningLanguage.Japanese, Token));
        }

        [Fact]
        public async Task Start_UnknownLearningDuplicateOrEmptySelectionIsRejected()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            await LearningWrongAsync(env, shared: false, finish: false);
            var error = Assert.Single(await Query(env).LoadAsync(LearningLanguage.Japanese, Token));
            foreach (var groups in new[] { Array.Empty<string>(), new[] { "不存在" }, new[] { error.WordGroupId }, new[] { error.WordGroupId, error.WordGroupId } })
                await Assert.ThrowsAsync<ArgumentException>(() => Practice(env).StartOrResumeAsync(LearningLanguage.Japanese, groups, Token));
            Assert.Null(await env.Provider.GetRequiredService<IReviewSessionReader>().GetActiveAsync(LearningLanguage.Japanese, Token));
        }

        [Fact]
        public async Task LoadAndStart_InvalidLanguageAndCancellationAreNotSwallowed()
        {
            using var env = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Query(env).LoadAsync((LearningLanguage)99, Token));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Practice(env).StartOrResumeAsync((LearningLanguage)99, new[] { "组" }, Token));
            await Assert.ThrowsAsync<ArgumentNullException>(() => Practice(env).StartOrResumeAsync(LearningLanguage.Japanese, null!, Token));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Query(env).LoadAsync(LearningLanguage.Japanese, cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Practice(env).StartOrResumeAsync(LearningLanguage.Japanese, new[] { "组" }, cancellation.Token));
        }

        private static async Task<LearningSessionDto> LearningWrongAsync(MemorySeedTestEnvironment env, bool shared, bool finish)
        {
            await env.ImportAsync();
            if (shared) await env.ImportAsync(sense: "jmdict:1:2");
            await env.Provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null, new[] { "Dictionary" },
                new[] { LearningChannel.Dictation }, new[] { LearningOutputMode.Kana }), Token);
            var session = await env.Sessions.StartOrResumeAsync(new(LearningLanguage.Japanese), Token);
            var q = Assert.IsType<PracticeQuestionDto>(await env.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            var result = await env.Sessions.SubmitAnswerAsync(new(session.SessionId, q.QuestionId, "错误"), Token);
            if (finish)
                while (result.NextQuestion is { } next)
                    result = await env.Sessions.SubmitAnswerAsync(new(session.SessionId, next.QuestionId, "たべる"), Token);
            return session;
        }

        private static Task<ReviewPlanDto> ReviewPlanAsync(MemorySeedTestEnvironment env, params string[] forms) =>
            env.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                ReviewRangeMode.All, Array.Empty<string>(), forms, new[] { LearningChannel.Dictation },
                new[] { LearningOutputMode.Kana }, EnglishFormLabelMode.GrammarName, null), Token);
    }
}
