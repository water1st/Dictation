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
    /// <summary>用真实 SQLite 验证复习客观成绩、间隔、去重及记忆的原子提交。</summary>
    public sealed class ReviewAnswerServiceTests
    {
        [Fact]
        public async Task Submit_TwoCorrectAnswersCompleteOneGroupAndSeedOneGoodMemory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var first = await QuestionAsync(environment, session.SessionId);
            var outcome = await CorrectAsync(environment, first);
            Assert.True(outcome.WasCorrect);
            Assert.Equal("たべる", outcome.ExpectedAnswer);
            Assert.Equal("たべる", outcome.ExpectedSpokenText);
            Assert.Null(outcome.Summary);
            Assert.Empty(await environment.MemoriesAsync(session.WordGroupIds[0]));
            var next = Assert.IsType<PracticeQuestionDto>(outcome.NextQuestion);
            var final = await CorrectAsync(environment, next);
            var summary = Assert.IsType<ReviewSessionSummaryDto>(final.Summary);
            Assert.Equal(1, summary.CompletedWordGroupCount);
            Assert.Equal(2, summary.AnsweredCount);
            Assert.Empty(summary.Errors);
            Assert.Null(final.NextQuestion);
            Assert.Null(await Reader(environment).GetCurrentQuestionAsync(session.SessionId, Token));
            var memory = Assert.Single(await environment.MemoriesAsync(session.WordGroupIds[0]));
            Assert.Equal(1L, (long)memory.ReviewCount);
            Assert.Equal(0L, (long)memory.LapseCount);
            Assert.Equal(2.3065d, (double)memory.Stability, precision: 5);
            Assert.Equal(0L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM LearningFormStates;"));
        }

        [Theory]
        [InlineData(null)] [InlineData("")] [InlineData("故意写错")]
        public async Task Submit_WrongThenTwoCorrectKeepsAgainAndErrorInFinalSummary(string? text)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var question = await QuestionAsync(environment, session.SessionId);
            var wrong = await Answerer(environment).SubmitAnswerAsync(new(session.SessionId, question.QuestionId, text), Token);
            Assert.False(wrong.WasCorrect);
            Assert.Equal("たべる", wrong.ExpectedAnswer);
            Assert.True(wrong.HadErrorInSession);
            var firstCorrect = await CorrectAsync(environment, Assert.IsType<PracticeQuestionDto>(wrong.NextQuestion));
            Assert.Null(firstCorrect.Summary);
            var final = await CorrectAsync(environment, Assert.IsType<PracticeQuestionDto>(firstCorrect.NextQuestion));
            var summary = Assert.IsType<ReviewSessionSummaryDto>(final.Summary);
            Assert.Equal(3, summary.AnsweredCount);
            Assert.Equal("Dictionary", Assert.Single(summary.Errors).FormCode);
            Assert.True(final.HadErrorInSession);
            var memory = Assert.Single(await environment.MemoriesAsync(session.WordGroupIds[0]));
            Assert.Equal(1L, (long)memory.ReviewCount);
            Assert.Equal(1L, (long)memory.LapseCount);
            Assert.Equal(0.212d, (double)memory.Stability, precision: 5);
        }

        [Fact]
        public async Task Submit_CoversTwoChannelsAndScriptsInTwoAnswersNotFourCombinations()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync(decision: ImportDecision.Review);
            await SaveAsync(environment, Plan() with
            {
                Channels = new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
                OutputModes = new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji }
            });
            var session = await StartExistingAsync(environment);
            var first = await QuestionAsync(environment, session.SessionId);
            var outcome = await CorrectAsync(environment, first);
            var second = Assert.IsType<PracticeQuestionDto>(outcome.NextQuestion);
            Assert.NotEqual(first.Channel, second.Channel);
            Assert.NotEqual(first.OutputMode, second.OutputMode);
            var final = await CorrectAsync(environment, second);
            Assert.Equal(2, Assert.IsType<ReviewSessionSummaryDto>(final.Summary).AnsweredCount);
            Assert.Single(await environment.MemoriesAsync(session.WordGroupIds[0]));
        }

        [Fact]
        public async Task Submit_WeakTeOnlyUpdatesItsExistingMemoryNotFutureDictionary()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            await SaveAsync(environment, Plan() with { FormCodes = new[] { "Dictionary", "Te" } });
            await ExecuteAsync(environment, """
                INSERT INTO ReviewFormMemories
                SELECT @Group, MemberId, 'Dictionary', 20, 5, @Last, @Future, 1, 0 FROM WordGroupMembers WHERE WordGroupId=@Group;
                INSERT INTO ReviewFormMemories
                SELECT @Group, MemberId, 'Te', 2, 5, @Last, @Past, 1, 0 FROM WordGroupMembers WHERE WordGroupId=@Group;
                """, new { Group = group, Last = MemorySeedTestEnvironment.Now.AddDays(-5).ToString("O"),
                    Future = MemorySeedTestEnvironment.Now.AddDays(10).ToString("O"), Past = MemorySeedTestEnvironment.Now.AddDays(-1).ToString("O") });
            var session = await StartExistingAsync(environment);
            var question = await QuestionAsync(environment, session.SessionId);
            Assert.Equal("Te", question.FormCode);
            var wrong = await Answerer(environment).SubmitAnswerAsync(new(session.SessionId, question.QuestionId, "错误"), Token);
            Assert.Equal("たべて", wrong.ExpectedAnswer);
            var firstCorrect = await CorrectAsync(environment, Assert.IsType<PracticeQuestionDto>(wrong.NextQuestion));
            var final = await CorrectAsync(environment, Assert.IsType<PracticeQuestionDto>(firstCorrect.NextQuestion));
            Assert.NotNull(final.Summary);
            var memories = await environment.MemoriesAsync(group);
            var dictionary = Assert.Single(memories, value => (string)value.FormCode == "Dictionary");
            var te = Assert.Single(memories, value => (string)value.FormCode == "Te");
            Assert.Equal(20d, (double)dictionary.Stability);
            Assert.Equal(1L, (long)dictionary.ReviewCount);
            Assert.Equal(2L, (long)te.ReviewCount);
            Assert.Equal(1L, (long)te.LapseCount);
        }

        [Fact]
        public async Task Submit_ErrorInOneFormDoesNotPenalizeOtherForm()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment, forms: new[] { "Dictionary", "Polite" });
            var summary = await FinishAsync(environment, session.SessionId, "Polite");
            Assert.Equal("Polite", Assert.Single(summary.Errors).FormCode);
            var memories = await environment.MemoriesAsync(session.WordGroupIds[0]);
            Assert.Equal(2, memories.Count);
            Assert.All(memories, memory => Assert.Equal((string)memory.FormCode == "Polite" ? 1L : 0L, (long)memory.LapseCount));
        }

        [Fact]
        public async Task Submit_RepeatedQuestionIsRejectedWithoutCountingOrRatingAgain()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var first = await QuestionAsync(environment, session.SessionId);
            var outcome = await CorrectAsync(environment, first);
            Assert.True(outcome.WasCorrect);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Answerer(environment).SubmitAnswerAsync(new(session.SessionId, first.QuestionId, "たべる"), Token));
            Assert.Equal(1L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM ReviewAnswerAttempts;"));
            Assert.Empty(await environment.MemoriesAsync(session.WordGroupIds[0]));
        }

        [Fact]
        public async Task Submit_ConcurrentIdenticalRequestsApplyExactlyOneAnswer()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var question = await QuestionAsync(environment, session.SessionId);
            var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                try
                {
                    var result = await Answerer(environment).SubmitAnswerAsync(new(session.SessionId, question.QuestionId, "たべる"), Token);
                    Assert.True(result.WasCorrect);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }, Token));
            var results = await Task.WhenAll(tasks);
            Assert.Single(results, success => success);
            Assert.Equal(1L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM ReviewAnswerAttempts;"));
            Assert.Equal(1L, await ScalarAsync<long>(environment, "SELECT SUM(CorrectCount) FROM ReviewSessionForms;"));
        }

        [Fact]
        public async Task Submit_MemoryFailureRollsBackAnswerStreakAndCompletionThenRetrySucceeds()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var first = await CorrectAsync(environment, await QuestionAsync(environment, session.SessionId));
            var pending = Assert.IsType<PracticeQuestionDto>(first.NextQuestion);
            await ExecuteAsync(environment, "CREATE TRIGGER RejectMemory BEFORE INSERT ON ReviewFormMemories BEGIN SELECT RAISE(ABORT,'记忆故障'); END;");
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => CorrectAsync(environment, pending));
            Assert.Equal(1L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM ReviewAnswerAttempts;"));
            Assert.Equal(1L, await ScalarAsync<long>(environment, "SELECT CorrectStreak FROM ReviewSessionForms;"));
            Assert.Empty(await environment.MemoriesAsync(session.WordGroupIds[0]));
            Assert.Equal(pending.QuestionId, (await QuestionAsync(environment, session.SessionId)).QuestionId);
            await ExecuteAsync(environment, "DROP TRIGGER RejectMemory;");
            var final = await CorrectAsync(environment, pending);
            Assert.NotNull(final.Summary);
            Assert.Single(await environment.MemoriesAsync(session.WordGroupIds[0]));
        }

        [Fact]
        public async Task Submit_NextQuestionFailureRollsBackCurrentAnswerAndOrdinal()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var pending = await QuestionAsync(environment, session.SessionId);
            await ExecuteAsync(environment, "CREATE TRIGGER RejectNext BEFORE INSERT ON ReviewQuestions BEGIN SELECT RAISE(ABORT,'下一题故障'); END;");
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => CorrectAsync(environment, pending));
            Assert.Equal(0L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM ReviewAnswerAttempts;"));
            Assert.Equal(0L, await ScalarAsync<long>(environment, "SELECT CurrentOrdinal FROM ReviewSessions;"));
            Assert.Equal(pending.QuestionId, (await QuestionAsync(environment, session.SessionId)).QuestionId);
        }

        [Fact]
        public async Task Submit_RecreatedContainerContinuesSavedStreakAndQuestion()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var outcome = await CorrectAsync(environment, await QuestionAsync(environment, session.SessionId));
            var pending = Assert.IsType<PracticeQuestionDto>(outcome.NextQuestion);
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(environment.Provider.GetRequiredService<ApplicationPaths>().Root).AddDictationCore();
            services.AddSingleton(environment.Provider.GetRequiredService<TimeProvider>());
            using var reopened = services.BuildServiceProvider();
            var restored = Assert.IsType<PracticeQuestionDto>(await reopened.GetRequiredService<IReviewQuestionReader>().GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Equal(pending.QuestionId, restored.QuestionId);
            var final = await reopened.GetRequiredService<IReviewSessionAnswerer>().SubmitAnswerAsync(new(session.SessionId, restored.QuestionId, "たべる"), Token);
            Assert.NotNull(final.Summary);
            Assert.Single(await environment.MemoriesAsync(session.WordGroupIds[0]));
        }

        [Fact]
        public async Task Submit_IdenticalDictationAcrossSensesSharesAnswersButKeepsSeparateMemory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var firstGroup = await ImportEnglishAsync(environment, "go%2:01:00::");
            var secondGroup = await ImportEnglishAsync(environment, "ecdict:go:v");
            await SaveAsync(environment, Plan() with { Language = LearningLanguage.English, FormCodes = new[] { "Base" },
                Channels = new[] { LearningChannel.Dictation }, OutputModes = new[] { LearningOutputMode.Latin } });
            var session = await StartExistingAsync(environment, LearningLanguage.English);
            var summary = await FinishAsync(environment, session.SessionId);
            Assert.Equal(2, summary.CompletedWordGroupCount);
            Assert.Equal(4, summary.AnsweredCount);
            Assert.Equal(2, (await environment.MemoriesAsync(firstGroup)).Count);
            Assert.Single(await environment.MemoriesAsync(secondGroup));
            Assert.Equal(6L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM ReviewAnswerAttempts;"));
        }

        [Fact]
        public async Task Submit_DeletingPrimaryDuplicateGroupDoesNotDeleteOtherGroupsAnswerHistory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var firstGroup = await ImportEnglishAsync(environment, "go%2:01:00::");
            var secondGroup = await ImportEnglishAsync(environment, "ecdict:go:v");
            await SaveAsync(environment, Plan() with { Language = LearningLanguage.English, FormCodes = new[] { "Base" },
                Channels = new[] { LearningChannel.Dictation }, OutputModes = new[] { LearningOutputMode.Latin } });
            var session = await StartExistingAsync(environment, LearningLanguage.English);
            var question = await QuestionAsync(environment, session.SessionId);
            for (var index = 0; question.SpokenText != "go" && index < 10; index++)
            {
                question = Assert.IsType<PracticeQuestionDto>((await CorrectAsync(environment, question)).NextQuestion);
            }
            Assert.Equal("go", question.SpokenText);
            Assert.True((await CorrectAsync(environment, question)).WasCorrect);
            var remaining = question.WordGroupId == firstGroup ? secondGroup : firstGroup;
            await environment.Provider.GetRequiredService<IReviewLibraryManager>().DeleteWordGroupAsync(LearningLanguage.English, question.WordGroupId, Token);
            Assert.Equal(1L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM ReviewAnswerAttempts WHERE WordGroupId=@Id AND FormCode='Base' AND MemberId IN (SELECT MemberId FROM WordGroupMembers WHERE Headword='go');", new { Id = remaining }));
            var summary = await FinishAsync(environment, session.SessionId);
            Assert.Equal(1, summary.CompletedWordGroupCount);
            Assert.NotEmpty(await environment.MemoriesAsync(remaining));
        }

        [Fact]
        public async Task Submit_CorrectAnswersForDifferentFormsInterleaveBeforeRetry()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment, new[] { "Dictionary", "Polite", "Te", "Ta" });
            var first = await QuestionAsync(environment, session.SessionId);
            var outcome = await CorrectAsync(environment, first);
            var next = Assert.IsType<PracticeQuestionDto>(outcome.NextQuestion);
            Assert.NotEqual(first.FormCode, next.FormCode);
            Assert.Equal(1L, await ScalarAsync<long>(environment, "SELECT CorrectStreak FROM ReviewSessionForms WHERE FormCode=@Code;", new { Code = first.FormCode }));
        }

        [Theory]
        [InlineData(null)] [InlineData("")] [InlineData(" ")]
        public async Task Submit_EmptySessionIdRejectsWithoutWriting(string? sessionId)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAnyAsync<ArgumentException>(() => Answerer(environment).SubmitAnswerAsync(new(sessionId!, 1, "答案"), Token));
            Assert.Equal(0L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM ReviewAnswerAttempts;"));
        }

        [Fact]
        public async Task Submit_UnknownQuestionAndSessionDoNotInventProgress()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            await QuestionAsync(environment, session.SessionId);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Answerer(environment).SubmitAnswerAsync(new(session.SessionId, 99999, "答案"), Token));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Answerer(environment).SubmitAnswerAsync(new("missing", 1, "答案"), Token));
            Assert.Equal(0L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM ReviewAnswerAttempts;"));
        }

        [Fact]
        public async Task Submit_CancelledOperationLeavesPendingAndMemoryUntouched()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var pending = await QuestionAsync(environment, session.SessionId);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Answerer(environment).SubmitAnswerAsync(new(session.SessionId, pending.QuestionId, "たべる"), cancellation.Token));
            Assert.Equal(0L, await ScalarAsync<long>(environment, "SELECT COUNT(*) FROM ReviewAnswerAttempts;"));
            Assert.Equal(pending.QuestionId, (await QuestionAsync(environment, session.SessionId)).QuestionId);
        }

        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static IReviewSessionAnswerer Answerer(MemorySeedTestEnvironment environment) => environment.Provider.GetRequiredService<IReviewSessionAnswerer>();
        private static IReviewQuestionReader Reader(MemorySeedTestEnvironment environment) => environment.Provider.GetRequiredService<IReviewQuestionReader>();
        private static SaveReviewPlanRequestDto Plan() => new(LearningLanguage.Japanese, 20, 20, ReviewRangeMode.All, Array.Empty<string>(),
            new[] { "Dictionary" }, new[] { LearningChannel.MeaningWriting }, new[] { LearningOutputMode.Kana }, EnglishFormLabelMode.GrammarName, null);
        private static Task<ReviewPlanDto> SaveAsync(MemorySeedTestEnvironment environment, SaveReviewPlanRequestDto plan) => environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(plan, Token);

        private static async Task<ReviewSessionDto> StartAsync(MemorySeedTestEnvironment environment, string[]? forms = null)
        {
            await environment.ImportAsync(decision: ImportDecision.Review);
            await SaveAsync(environment, Plan() with { FormCodes = forms ?? new[] { "Dictionary" } });
            return await StartExistingAsync(environment);
        }

        private static async Task<ReviewSessionDto> StartExistingAsync(MemorySeedTestEnvironment environment, LearningLanguage language = LearningLanguage.Japanese) =>
            Assert.IsType<ReviewSessionDto>(await environment.Provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(language, Token));
        private static async Task<PracticeQuestionDto> QuestionAsync(MemorySeedTestEnvironment environment, string sessionId) =>
            Assert.IsType<PracticeQuestionDto>(await Reader(environment).GetCurrentQuestionAsync(sessionId, Token));

        private static async Task<ReviewAnswerResultDto> CorrectAsync(MemorySeedTestEnvironment environment, PracticeQuestionDto question)
        {
            var text = await ScalarAsync<string>(environment, """
                SELECT Forms.Surface FROM WordGroupForms AS Forms INNER JOIN ReviewQuestions AS Questions ON Questions.MemberId=Forms.MemberId
                WHERE Questions.QuestionId=@QuestionId AND Forms.FormCode=Questions.FormCode
                    AND (Questions.OutputMode='Latin' OR Forms.ScriptCode=Questions.OutputMode)
                ORDER BY Forms.IsPrimary DESC, Forms.IsCommon DESC, Forms.FormIndex LIMIT 1;
                """, new { question.QuestionId });
            var result = await Answerer(environment).SubmitAnswerAsync(new(question.SessionId, question.QuestionId, text), Token);
            Assert.True(result.WasCorrect);
            Assert.Equal(text, result.ExpectedAnswer);
            return result;
        }

        private static async Task<ReviewSessionSummaryDto> FinishAsync(MemorySeedTestEnvironment environment, string sessionId, string? errorForm = null)
        {
            var question = await QuestionAsync(environment, sessionId);
            var madeError = false;
            for (var index = 0; index < 100; index++)
            {
                ReviewAnswerResultDto result;
                if (!madeError && question.FormCode == errorForm)
                {
                    madeError = true;
                    result = await Answerer(environment).SubmitAnswerAsync(new(sessionId, question.QuestionId, "故意写错"), Token);
                    Assert.False(result.WasCorrect);
                    Assert.False(string.IsNullOrWhiteSpace(result.ExpectedAnswer));
                }
                else
                {
                    result = await CorrectAsync(environment, question);
                }
                if (result.Summary is not null)
                {
                    if (errorForm is not null) Assert.True(madeError);
                    return result.Summary;
                }
                question = Assert.IsType<PracticeQuestionDto>(result.NextQuestion);
            }
            throw new InvalidOperationException("复习会话在100次以内没有完成。");
        }

        private static async Task<string> ImportEnglishAsync(MemorySeedTestEnvironment environment, string sense)
        {
            var batch = await environment.Provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(
                LearningLanguage.English, ImportSourceKind.Manual, new[] { new ImportSelectionDto("go", sense) }), Token);
            var item = Assert.Single(batch.Batch.Items);
            await environment.Provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(batch.Batch.BatchId, new[] { item.ItemId }, ImportDecision.Review), Token);
            return item.WordGroupId!;
        }

        private static async Task<T> ScalarAsync<T>(MemorySeedTestEnvironment environment, string sql, object? arguments = null)
        {
            await using var connection = environment.Connections.CreateConnection();
            return (await connection.ExecuteScalarAsync<T>(sql, arguments))!;
        }

        private static async Task ExecuteAsync(MemorySeedTestEnvironment environment, string sql, object? arguments = null)
        {
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            await connection.ExecuteAsync(sql, arguments);
        }
    }
}
