using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Practice;
using Dictation.Core.Review;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>通过真实三层验证独立复习待答题的提示、恢复、原子创建及只读成绩边界。</summary>
    public sealed class ReviewQuestionServiceTests
    {
        [Fact]
        public async Task Current_MeaningWritingShowsChineseAndRequirementsWithoutSpokenAnswer()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment, form: "Polite");
            var question = Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(LearningLanguage.Japanese, question.Language);
            Assert.Equal(LearningChannel.MeaningWriting, question.Channel);
            Assert.Equal("Polite", question.FormCode);
            Assert.Contains("礼貌", question.FormLabel);
            Assert.Equal("假名", question.OutputLabel);
            Assert.Contains("吃", question.Meanings);
            Assert.Null(question.SpokenText);
            Assert.Equal(0, question.QuestionOrdinal);
            Assert.Equal(1L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewQuestions WHERE Status='Pending';"));
        }

        [Theory]
        [InlineData(LearningOutputMode.Kana)]
        [InlineData(LearningOutputMode.Kanji)]
        public async Task Current_PoliteDictationReadsActualKanaNotGrammarOrDictionaryForm(LearningOutputMode output)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment, form: "Polite", channel: LearningChannel.Dictation, output: output);
            var question = Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(output, question.OutputMode);
            Assert.Equal("たべます", question.SpokenText);
            Assert.Contains("礼貌", question.FormLabel);
        }

        [Fact]
        public async Task Current_RepeatedReadAndNewContainerResumeExactlyOnePendingQuestion()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var first = Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(environment.Provider.GetRequiredService<ApplicationPaths>().Root).AddDictationCore();
            using var reopened = services.BuildServiceProvider();
            var restored = Assert.IsType<PracticeQuestionDto>(await reopened.GetRequiredService<IReviewQuestionReader>().GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equivalent(first, restored);
            Assert.Equal(1L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewQuestions;"));
        }

        [Fact]
        public async Task Current_PlanEditedBeforeFirstQuestionStillUsesPersistedStartupSnapshot()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment, form: "Polite");
            await environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Plan() with
            {
                FormCodes = new[] { "Te" }, Channels = new[] { LearningChannel.Dictation }, OutputModes = new[] { LearningOutputMode.Kanji }
            }, Token);
            var question = Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal("Polite", question.FormCode);
            Assert.Equal(LearningChannel.MeaningWriting, question.Channel);
            Assert.Equal(LearningOutputMode.Kana, question.OutputMode);
            Assert.Null(question.SpokenText);
        }

        [Fact]
        public async Task Current_ConcurrentReadersReturnTheSameDurableWinner()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token), Token)));
            Assert.All(results, value => Assert.IsType<PracticeQuestionDto>(value));
            Assert.Single(results.Select(value => value!.QuestionId).Distinct());
            Assert.Equal(1L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewQuestions;"));
        }

        [Fact]
        public async Task Current_DoesNotAdvanceLearningMemoryOrDailyCompletion()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM LearningQuestions;"));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM LearningFormStates;"));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewFormMemories;"));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT SUM(CorrectCount+ErrorCount+CorrectStreak+IsMastered) FROM ReviewSessionForms;"));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewSessionGroups WHERE CompletedLocalDate IS NOT NULL;"));
        }

        [Fact]
        public async Task Current_RareKanjiWithBothOutputsUsesOnlyAvailableKana()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync("跪く", "jmdict:6:1", ImportDecision.Review);
            await environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Plan() with { OutputModes = new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji } }, Token);
            var session = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
            var question = Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(LearningOutputMode.Kana, question.OutputMode);
        }

        [Fact]
        public async Task Current_CreatingQuestionFailureRollsBackAndRetryDoesNotSkipOrdinal()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            await ExecuteAsync(environment, "CREATE TRIGGER RejectReviewQuestion BEFORE INSERT ON ReviewQuestions BEGIN SELECT RAISE(ABORT,'测试写入失败'); END;");
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewQuestions;"));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT CurrentOrdinal FROM ReviewSessions;"));
            await ExecuteAsync(environment, "DROP TRIGGER RejectReviewQuestion;");
            var question = Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(0, question.QuestionOrdinal);
        }

        [Fact]
        public async Task Current_DeleteWholeGroupCascadesPendingAndReturnsNoQuestion()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            await environment.Provider.GetRequiredService<IReviewLibraryManager>().DeleteWordGroupAsync(LearningLanguage.Japanese, session.WordGroupIds[0], Token);
            Assert.Null(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewQuestions;"));
        }

        [Fact]
        public async Task Current_CompletedSessionReturnsNoNewPendingQuestion()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            await ExecuteAsync(environment, "UPDATE ReviewQuestions SET Status='Answered'; UPDATE ReviewSessions SET Status='Completed';");
            Assert.Null(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(1L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewQuestions;"));
        }

        [Fact]
        public async Task Current_EnglishPastUsesChineseMeaningsAndItsOwnLexemeSound()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await ImportEnglishAsync(environment);
            await environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Plan() with
            {
                Language = LearningLanguage.English, FormCodes = new[] { "Past" }, Channels = new[] { LearningChannel.Dictation }, OutputModes = new[] { LearningOutputMode.Latin }
            }, Token);
            var session = Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.English, Token));
            var question = Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(LearningLanguage.English, question.Language);
            Assert.Equal("过去式", question.FormLabel);
            Assert.Contains(question.SpokenText, new[] { "went", "converted" });
            Assert.Contains(question.Meanings, value => value.Contains(question.SpokenText == "went" ? "去" : "转换", StringComparison.Ordinal));
            Assert.Equal(2L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewSessionForms;"));
        }

        [Theory]
        [InlineData(null)] [InlineData("")] [InlineData(" ")]
        public async Task Current_EmptyIdRejectsBeforeDatabaseMutation(string? sessionId)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAnyAsync<ArgumentException>(() => Reader(environment).GetCurrentQuestionAsync(sessionId!, Token));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewQuestions;"));
        }

        [Fact]
        public async Task Current_UnknownSessionRejectsRatherThanInventingProgress()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Reader(environment).GetCurrentQuestionAsync("missing", Token));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewQuestions;"));
        }

        [Fact]
        public async Task Current_CancellationPropagatesWithoutCreatingQuestion()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader(environment).GetCurrentQuestionAsync(session.SessionId, cancellation.Token));
            Assert.Equal(0L, await ScalarAsync(environment, "SELECT COUNT(*) FROM ReviewQuestions;"));
        }

        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static IReviewQuestionReader Reader(MemorySeedTestEnvironment environment) => environment.Provider.GetRequiredService<IReviewQuestionReader>();
        private static IReviewSessionStarter Starter(MemorySeedTestEnvironment environment) => environment.Provider.GetRequiredService<IReviewSessionStarter>();
        private static SaveReviewPlanRequestDto Plan() => new(LearningLanguage.Japanese, 20, 20, ReviewRangeMode.All, Array.Empty<string>(),
            new[] { "Dictionary" }, new[] { LearningChannel.MeaningWriting }, new[] { LearningOutputMode.Kana }, EnglishFormLabelMode.GrammarName, null);

        private static async Task<ReviewSessionDto> StartAsync(MemorySeedTestEnvironment environment, string form = "Dictionary",
            LearningChannel channel = LearningChannel.MeaningWriting, LearningOutputMode output = LearningOutputMode.Kana)
        {
            await environment.ImportAsync(decision: ImportDecision.Review);
            await environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(Plan() with
            { FormCodes = new[] { form }, Channels = new[] { channel }, OutputModes = new[] { output } }, Token);
            return Assert.IsType<ReviewSessionDto>(await Starter(environment).StartOrResumeAsync(LearningLanguage.Japanese, Token));
        }

        private static async Task ImportEnglishAsync(MemorySeedTestEnvironment environment)
        {
            var batch = await environment.Provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(
                LearningLanguage.English, ImportSourceKind.Manual, new[] { new ImportSelectionDto("go", "go%2:01:00::") }), Token);
            await environment.Provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(
                batch.Batch.BatchId, new[] { Assert.Single(batch.Batch.Items).ItemId }, ImportDecision.Review), Token);
        }

        private static async Task<long> ScalarAsync(MemorySeedTestEnvironment environment, string sql)
        {
            await using var connection = environment.Connections.CreateConnection();
            return await connection.ExecuteScalarAsync<long>(sql);
        }

        private static async Task ExecuteAsync(MemorySeedTestEnvironment environment, string sql)
        {
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            await connection.ExecuteAsync(sql);
        }
    }
}
