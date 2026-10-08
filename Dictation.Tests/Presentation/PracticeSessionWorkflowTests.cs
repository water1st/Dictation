using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Practice;
using Dictation.Core.Review;
using Dictation.DataAccess.Learning;
using Dictation.Presentation.Practice;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>通过真实业务与数据库验证共用练习界面的模式适配，不把复习成绩写入学习。</summary>
    public sealed class PracticeSessionWorkflowTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task StartAndAnswer_KeepNativeModeAndMapFeedbackWithoutLosingErrors(bool review)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: review ? ImportDecision.Review : ImportDecision.Learning);
            await environment.SavePlanAsync("Polite");
            await SaveReviewAsync(environment, "Polite");
            var workflow = Create(environment, review);
            Assert.Equal(review ? "复习" : "学习", workflow.ModeLabel);
            var session = Assert.IsType<PracticeSessionSnapshot>(await workflow.StartOrResumeAsync(LearningLanguage.Japanese, null, Token));
            Assert.Equal(LearningLanguage.Japanese, session.Language);
            Assert.Equal(new[] { group }, session.WordGroupIds);
            Assert.Equal(new[] { "Polite" }, session.FormCodes);
            Assert.False(session.IsCompleted);
            var members = await workflow.GetBrowseItemsAsync(session.SessionId, Token);
            Assert.Equal("食べる", Assert.Single(members).Headword);
            Assert.Contains("吃", members[0].Meanings);
            var question = Assert.IsType<PracticeQuestionDto>(await workflow.GetCurrentQuestionAsync(session.SessionId, Token));
            Assert.Null(question.SpokenText);
            var wrong = await workflow.SubmitAnswerAsync(session.SessionId, question.QuestionId, "错误", Token);
            Assert.False(wrong.WasCorrect);
            Assert.True(wrong.HadErrorInSession);
            Assert.Equal("たべます", wrong.CorrectAnswer);
            Assert.Equal("たべます", wrong.SpokenText);
            Assert.Null(wrong.Summary);
            var next = Assert.IsType<PracticeQuestionDto>(wrong.NextQuestion);
            var correct = await workflow.SubmitAnswerAsync(session.SessionId, next.QuestionId, "たべます", Token);
            Assert.True(correct.WasCorrect);
            Assert.True(correct.HadErrorInSession);
            Assert.Null(correct.Summary);
            next = Assert.IsType<PracticeQuestionDto>(correct.NextQuestion);
            var final = await workflow.SubmitAnswerAsync(session.SessionId, next.QuestionId, "たべます", Token);
            var summary = Assert.IsType<PracticeSessionSummary>(final.Summary);
            Assert.Equal(session.SessionId, summary.SessionId);
            Assert.Equal(3, summary.AnsweredCount);
            Assert.Equal(1, summary.CompletedWordGroupCount);
            var error = Assert.Single(summary.Errors);
            Assert.Equal(group, error.WordGroupId);
            Assert.Equal("Polite", error.FormCode);
            Assert.Equal("たべます", error.CorrectAnswer);
            Assert.Equal(review ? await environment.MemberAsync(group) : (long?)null, error.MemberId);
            Assert.Null(final.NextQuestion);
            Assert.Null(await workflow.GetCurrentQuestionAsync(session.SessionId, Token));
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            Assert.Equal(review ? 0 : 3, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            Assert.Equal(review ? 3 : 0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ReviewAnswerAttempts;"));
        }

        [Fact]
        public async Task ReviewResume_UsesSavedFormsAndBrowseDoesNotAdvanceMemory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            await SaveReviewAsync(environment, "Polite");
            var workflow = Create(environment, true);
            var first = Assert.IsType<PracticeSessionSnapshot>(await workflow.StartOrResumeAsync(LearningLanguage.Japanese, null, Token));
            var question = await workflow.GetCurrentQuestionAsync(first.SessionId, Token);
            await SaveReviewAsync(environment, "Dictionary");
            var resumed = Assert.IsType<PracticeSessionSnapshot>(await Create(environment, true).StartOrResumeAsync(LearningLanguage.Japanese, null, Token));
            Assert.Equal(first.SessionId, resumed.SessionId);
            Assert.Equal(new[] { "Polite" }, resumed.FormCodes);
            var resumedQuestion = Assert.IsType<PracticeQuestionDto>(await workflow.GetCurrentQuestionAsync(resumed.SessionId, Token));
            Assert.Equal(question!.QuestionId, resumedQuestion.QuestionId);
            Assert.Equal(question.FormCode, resumedQuestion.FormCode);
            Assert.Equal(question.Meanings, resumedQuestion.Meanings);
            Assert.Single(await workflow.GetBrowseItemsAsync(resumed.SessionId, Token));
            Assert.Empty(await environment.MemoriesAsync(group));
        }

        [Fact]
        public async Task ReviewStart_NoEligibleWordReturnsNullRatherThanCompletedSession()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await SaveReviewAsync(environment, "Polite");
            Assert.Null(await Create(environment, true).StartOrResumeAsync(LearningLanguage.Japanese, null, Token));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CancelledStart_PropagatesCancellationWithoutCreatingSession(bool review)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(environment, review)
                .StartOrResumeAsync(LearningLanguage.Japanese, null, new CancellationToken(true)));
        }

        [Fact]
        public async Task ReviewStart_RejectsLearningManualSelectionRatherThanSilentlyIgnoringIt()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAsync<ArgumentException>(() => Create(environment, true)
                .StartOrResumeAsync(LearningLanguage.Japanese, new[] { "学习手选词组" }, Token));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadMissingSession_PropagatesBusinessFailure(bool review)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            if (review)
                await Assert.ThrowsAsync<KeyNotFoundException>(() => Create(environment, true).GetCurrentQuestionAsync("不存在", Token));
            else
                await Assert.ThrowsAsync<LearningSessionDataAccessException>(() => Create(environment, false).GetCurrentQuestionAsync("不存在", Token));
        }

        private static IPracticeSessionWorkflow Create(MemorySeedTestEnvironment environment, bool review)
        {
            var provider = environment.Provider;
            return review ? new ReviewPracticeWorkflow(provider.GetRequiredService<IReviewSessionStarter>(),
                provider.GetRequiredService<IReviewSessionReader>(), provider.GetRequiredService<IReviewQuestionReader>(),
                provider.GetRequiredService<IReviewSessionAnswerer>()) :
                new LearningPracticeWorkflow(provider.GetRequiredService<ILearningSessionOperations>(), provider.GetRequiredService<ILearningPlanReader>());
        }

        private static Task<ReviewPlanDto> SaveReviewAsync(MemorySeedTestEnvironment environment, params string[] forms) =>
            environment.Provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(new SaveReviewPlanRequestDto(
                LearningLanguage.Japanese, 20, 20, ReviewRangeMode.All, Array.Empty<string>(), forms,
                new[] { LearningChannel.MeaningWriting }, new[] { LearningOutputMode.Kana }, EnglishFormLabelMode.GrammarName, null), Token);
    }
}
