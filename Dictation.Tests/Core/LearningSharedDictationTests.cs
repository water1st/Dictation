using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Practice;
using Dictation.DataAccess.Learning;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>通过真实学习答题验证共享听写、独立记忆、删除隔离和原子失败边界。</summary>
    public sealed class LearningSharedDictationTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Submit_JapaneseIdenticalDictationCompletesBothSensesWithIndependentMemories(bool firstWrong)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var question = Assert.IsType<PracticeQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            if (firstWrong)
            {
                var wrong = await SubmitAsync(environment, question, "错误");
                Assert.False(wrong.WasCorrect);
                question = Assert.IsType<PracticeQuestionDto>(wrong.NextQuestion);
                Assert.Equal(2, await ScalarAsync<int>(environment, "SELECT SUM(ErrorCount) FROM LearningFormStates;"));
            }
            var first = await SubmitAsync(environment, question, "たべる");
            Assert.True(first.WasCorrect);
            Assert.Equal(2, await ScalarAsync<int>(environment, "SELECT SUM(CorrectStreak) FROM LearningFormStates;"));
            var final = await SubmitAsync(environment, Assert.IsType<PracticeQuestionDto>(first.NextQuestion), "たべる");
            var summary = Assert.IsType<LearningSessionSummaryDto>(final.Summary);
            Assert.Equal(firstWrong ? 3 : 2, summary.AnsweredCount);
            Assert.Equal(firstWrong ? 2 : 0, summary.Errors.Count);
            Assert.Equal(2, await ScalarAsync<int>(environment, "SELECT COUNT(*) FROM WordGroups WHERE Placement='Review';"));
            Assert.Equal(2, await ScalarAsync<int>(environment, "SELECT COUNT(*) FROM ReviewFormMemories;"));
            Assert.Equal(firstWrong ? 2 : 0, await ScalarAsync<int>(environment, "SELECT SUM(LapseCount) FROM ReviewFormMemories;"));
        }

        [Fact]
        public async Task Submit_EnglishIdenticalDictationProjectsOneObservationAcrossSenses()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            foreach (var sense in new[] { "go%2:01:00::", "ecdict:go:v" })
            {
                var staged = await environment.Provider.GetRequiredService<IImportStaging>().StageAsync(new(LearningLanguage.English,
                    ImportSourceKind.Manual, new[] { new ImportSelectionDto("go", sense) }), Token);
                await environment.Provider.GetRequiredService<IImportInboxActions>().DecideAsync(new(staged.Batch.BatchId,
                    new[] { Assert.Single(staged.Batch.Items).ItemId }, ImportDecision.Learning), Token);
            }
            await SavePlanAsync(environment, LearningLanguage.English, LearningChannel.Dictation);
            var session = await environment.Sessions.StartOrResumeAsync(new(LearningLanguage.English), Token);
            var question = Assert.IsType<PracticeQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            for (var index = 0; question.SpokenText != "go" && index < 6; index++)
                question = Assert.IsType<PracticeQuestionDto>((await SubmitAsync(environment, question, question.SpokenText)).NextQuestion);
            Assert.Equal("go", question.SpokenText);
            await SubmitAsync(environment, question, "go");
            Assert.Equal(2, await ScalarAsync<int>(environment, "SELECT SUM(CorrectStreak) FROM LearningFormStates WHERE MemberId IN (SELECT MemberId FROM WordGroupMembers WHERE Headword='go');"));
            Assert.Equal(1, await ScalarAsync<int>(environment, "SELECT COUNT(*) FROM LearningSharedAnswerAttempts;"));
        }

        [Fact]
        public async Task Submit_MeaningWritingRemainsIndependentForDifferentSenses()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment, LearningChannel.MeaningWriting);
            var question = Assert.IsType<PracticeQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            await SubmitAsync(environment, question, "たべる");
            Assert.Equal(1, await ScalarAsync<int>(environment, "SELECT SUM(CorrectStreak) FROM LearningFormStates;"));
            Assert.Equal(0, await ScalarAsync<int>(environment, "SELECT COUNT(*) FROM LearningSharedAnswerAttempts;"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Submit_DifferentSpokenTextOrAcceptedAnswersDoesNotMerge(bool changeSpoken)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment, prepareQuestion: false);
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            await connection.ExecuteAsync(changeSpoken
                ? "UPDATE WordGroupForms SET Reading='くう' WHERE MemberId IN (SELECT MemberId FROM WordGroupMembers WHERE SourceSenseId='jmdict:1:2') AND FormCode='Dictionary';"
                : "UPDATE WordGroupForms SET Surface='くう' WHERE MemberId IN (SELECT MemberId FROM WordGroupMembers WHERE SourceSenseId='jmdict:1:2') AND FormCode='Dictionary' AND ScriptCode='Kana';");
            var question = Assert.IsType<PracticeQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            await SubmitAsync(environment, question, await environment.CorrectAnswerAsync(question));
            Assert.Equal(1, await ScalarAsync<int>(environment, "SELECT SUM(CorrectStreak) FROM LearningFormStates;"));
            Assert.Equal(0, await ScalarAsync<int>(environment, "SELECT COUNT(*) FROM LearningSharedAnswerAttempts;"));
        }

        [Fact]
        public async Task Delete_PrimaryGroupPreservesOtherSenseWrongObservationAndAgainMemory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var question = Assert.IsType<PracticeQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            await SubmitAsync(environment, question, "错误");
            await environment.Provider.GetRequiredService<ILearningLibraryManager>().DeleteWordGroupAsync(LearningLanguage.Japanese, question.WordGroupId, Token);
            var snapshot = await environment.Provider.GetRequiredService<LearningSessionDataAccessor>().GetSchedulingSnapshotAsync(session.SessionId, Token);
            Assert.Contains(snapshot.History, value => value.WasCorrect == false);
            LearningSessionSummaryDto? summary = null;
            for (var index = 0; index < 10 && summary is null; index++)
            {
                var next = Assert.IsType<PracticeQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
                summary = (await SubmitAsync(environment, next, "たべる")).Summary;
            }
            Assert.NotNull(summary);
            Assert.Equal(1, await ScalarAsync<int>(environment, "SELECT SUM(LapseCount) FROM ReviewFormMemories;"));
        }

        [Fact]
        public async Task Submit_SecondaryObservationFailureRollsBackPrimaryAndAllTargetState()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var question = Assert.IsType<PracticeQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            await connection.ExecuteAsync("CREATE TRIGGER RejectShared BEFORE INSERT ON LearningSharedAnswerAttempts BEGIN SELECT RAISE(ABORT,'共享观察故障'); END;");
            await Assert.ThrowsAsync<LearningSessionDataAccessException>(() => SubmitAsync(environment, question, "たべる"));
            Assert.Equal(0, await ScalarAsync<int>(environment, "SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            Assert.Equal(0, await ScalarAsync<int>(environment, "SELECT SUM(CorrectStreak) FROM LearningFormStates;"));
            Assert.Equal(question.QuestionId, (await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token))!.QuestionId);
        }

        [Fact]
        public async Task Submit_CancelledOrRepeatedQuestionDoesNotAddSecondaryObservation()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var session = await StartAsync(environment);
            var question = Assert.IsType<PracticeQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => environment.Sessions.SubmitAnswerAsync(new(session.SessionId, question.QuestionId, "たべる"), cancellation.Token));
            Assert.Equal(0, await ScalarAsync<int>(environment, "SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            await SubmitAsync(environment, question, "たべる");
            await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(environment, question, "たべる"));
            Assert.Equal(1, await ScalarAsync<int>(environment, "SELECT COUNT(*) FROM LearningSharedAnswerAttempts;"));
        }

        private static async Task<LearningSessionDto> StartAsync(MemorySeedTestEnvironment environment,
            LearningChannel channel = LearningChannel.Dictation, bool prepareQuestion = true)
        {
            await environment.ImportAsync();
            await environment.ImportAsync(sense: "jmdict:1:2");
            await SavePlanAsync(environment, LearningLanguage.Japanese, channel);
            var session = await environment.Sessions.StartOrResumeAsync(new(LearningLanguage.Japanese), Token);
            if (prepareQuestion) await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, Token);
            return session;
        }

        private static Task<LearningPlanDto> SavePlanAsync(MemorySeedTestEnvironment environment, LearningLanguage language, LearningChannel channel) =>
            environment.Provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(new(language, 20, 20, LearningSelectionMode.Sequential,
                EnglishFormLabelMode.GrammarName, null, new[] { language == LearningLanguage.Japanese ? "Dictionary" : "Base" },
                new[] { channel }, new[] { language == LearningLanguage.Japanese ? LearningOutputMode.Kana : LearningOutputMode.Latin }), Token);

        private static Task<LearningAnswerResultDto> SubmitAsync(MemorySeedTestEnvironment environment, PracticeQuestionDto question, string? text) =>
            environment.Sessions.SubmitAnswerAsync(new(question.SessionId, question.QuestionId, text), Token);

        private static async Task<T> ScalarAsync<T>(MemorySeedTestEnvironment environment, string sql)
        {
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            return (await connection.ExecuteScalarAsync<T>(sql))!;
        }
    }
}
